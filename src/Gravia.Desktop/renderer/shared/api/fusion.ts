import { FusionHub } from '@actuallab/fusion-rpc';
import { RpcClientPeer } from '@actuallab/rpc';

const url = new URL('/rpc/ws', window.location.href);
url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';

export const fusionHub = new FusionHub();
export const fusionPeer = new RpcClientPeer(fusionHub, url.href, false);
fusionHub.addPeer(fusionPeer);

const close = () => fusionHub.close();
window.addEventListener('pagehide', close);
if (import.meta.hot) {
  import.meta.hot.dispose(() => {
    window.removeEventListener('pagehide', close);
    close();
  });
}
