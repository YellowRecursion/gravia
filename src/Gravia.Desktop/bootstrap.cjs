// This is a private Host-to-shell startup message, not the plugin or frontend API.
function parseBootstrap(line) {
  const value = JSON.parse(line);
  const url = new URL(value.backendUrl);
  if (url.protocol !== 'http:' || url.hostname !== '127.0.0.1' ||
      !url.port || url.username || url.password || url.pathname !== '/' || url.search || url.hash) {
    throw new Error('Expected a loopback backend URL with an explicit port.');
  }
  return { backendUrl: url.href };
}

module.exports = { parseBootstrap };
