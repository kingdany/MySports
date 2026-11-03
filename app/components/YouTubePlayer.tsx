import { StyleSheet } from 'react-native';
import { WebView } from 'react-native-webview';

import { DATA_URL } from '@/lib/data';

// Player ufficiale di YouTube (IFrame API) incorporato in una pagina minima.
// YouTube rifiuta le pagine senza un'origine web: baseUrl gliene dà una, quella dei nostri dati.
const ORIGIN = DATA_URL.match(/^https?:\/\/[^/]+/)![0]; // es. "https://kingdany.github.io"

// Gli errori del player (es. 101/150: il proprietario non permette la visione fuori da YouTube)
// vengono passati all'app, che può ripiegare sull'app YouTube.
const playerHtml = (videoId: string) => `<!doctype html>
<html>
<head>
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>html, body, #player { margin: 0; width: 100%; height: 100%; background: #000; }</style>
</head>
<body>
<div id="player"></div>
<script src="https://www.youtube.com/iframe_api"></script>
<script>
  function onYouTubeIframeAPIReady() {
    new YT.Player('player', {
      width: '100%',
      height: '100%',
      videoId: '${videoId}',
      playerVars: { autoplay: 1, playsinline: 1, rel: 0 },
      events: {
        onError: function (e) {
          window.ReactNativeWebView.postMessage(JSON.stringify({ type: 'error', code: e.data }));
        }
      }
    });
  }
</script>
</body>
</html>`;

export function YouTubePlayer({ videoId, onError }: { videoId: string; onError: (code: number) => void }) {
  return (
    <WebView
      style={styles.player}
      source={{ html: playerHtml(videoId), baseUrl: ORIGIN }}
      originWhitelist={['*']}
      allowsInlineMediaPlayback
      allowsFullscreenVideo
      mediaPlaybackRequiresUserAction={false}
      javaScriptEnabled
      onMessage={(e) => {
        try {
          const message = JSON.parse(e.nativeEvent.data);
          if (message.type === 'error') onError(Number(message.code));
        } catch {
          // messaggi non nostri: ignorati
        }
      }}
    />
  );
}

const styles = StyleSheet.create({
  player: { aspectRatio: 16 / 9, backgroundColor: '#000' },
});
