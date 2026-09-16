// This is a private Host-to-shell startup message, not the plugin or frontend API.
const path = require('node:path');

function parseBootstrap(line) {
  const value = JSON.parse(line);
  const url = new URL(value.backendUrl);
  if (
    url.protocol !== 'http:' ||
    url.hostname !== '127.0.0.1' ||
    !url.port ||
    url.username ||
    url.password ||
    url.pathname !== '/' ||
    url.search ||
    url.hash
  ) {
    throw new Error('Expected a loopback backend URL with an explicit port.');
  }
  if (value.sourceDirectory !== undefined) {
    if (typeof value.sourceDirectory !== 'string' || !path.isAbsolute(value.sourceDirectory))
      throw new Error('Expected an absolute desktop source directory.');
    return { backendUrl: url.href, sourceDirectory: path.normalize(value.sourceDirectory) };
  }
  return { backendUrl: url.href };
}

module.exports = { parseBootstrap };
