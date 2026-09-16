const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { randomBytes } = require('node:crypto');
const http = require('node:http');

/** Development only. Runs in the Electron main process, so no orphan npm/Vite child survives it. */
async function startDevServer(sourceDirectory, backendUrl) {
  const { createServer } = await import(
    pathToFileURL(require.resolve('vite', { paths: [sourceDirectory] })).href
  );
  const nonce = randomBytes(24).toString('base64');
  let server;
  const sockets = new Set();
  const httpServer = http.createServer((request, response) => {
    if (server) server.middlewares(request, response);
    else response.writeHead(503).end();
  });
  httpServer.on('connection', (socket) => {
    sockets.add(socket);
    socket.once('close', () => sockets.delete(socket));
  });
  await new Promise((resolve, reject) => {
    httpServer.once('error', reject);
    httpServer.listen(0, '127.0.0.1', resolve);
  });
  const address = httpServer.address();
  const url = `http://127.0.0.1:${address.port}/`;
  async function close() {
    const closed = new Promise((resolve) => httpServer.close(resolve));
    for (const socket of sockets) socket.destroy();
    await Promise.all([server?.close(), closed]);
  }
  try {
    server = await createServer({
      configFile: path.join(sourceDirectory, 'vite.config.ts'),
      mode: 'development',
      cacheDir: path.join(sourceDirectory, 'node_modules', '.vite'),
      clearScreen: false,
      html: { cspNonce: nonce },
      server: {
        host: '127.0.0.1',
        port: address.port,
        strictPort: true,
        open: false,
        cors: false,
        // Embedded mode: Electron owns termination. Vite must not attach stdin/SIGTERM exit hooks.
        middlewareMode: { server: httpServer },
        hmr: { server: httpServer, host: '127.0.0.1', clientPort: address.port },
        fs: { strict: true, allow: [sourceDirectory] },
        headers: {
          'Content-Security-Policy': `default-src 'none'; script-src 'self' 'nonce-${nonce}'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; font-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'`,
          'X-Content-Type-Options': 'nosniff',
        },
        proxy: {
          // Keep Host and Origin intact: the Host's same-origin validator also protects this route.
          '^/rpc/ws(?:\\?|$)': { target: backendUrl, ws: true, changeOrigin: false },
        },
      },
    });
    console.info(`Gravia Dev: ${url} (UI hot reload enabled)`);
    return { url, close };
  } catch (error) {
    await close();
    throw error;
  }
}

module.exports = { startDevServer };
