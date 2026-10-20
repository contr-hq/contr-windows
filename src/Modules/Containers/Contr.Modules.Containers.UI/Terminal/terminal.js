// Bridges xterm.js and the client: the client posts output and commands, the page posts input and its size.
const themes = {
  dark: { background: '#0c0c0c', foreground: '#cccccc', cursor: '#ffffff', selectionBackground: '#264f78' },
  light: { background: '#fbfbfb', foreground: '#1b1b1b', cursor: '#1b1b1b', selectionBackground: '#add6ff' },
};

const element = document.getElementById('terminal');
const terminal = new Terminal({
  fontFamily: "'Cascadia Mono', Consolas, monospace",
  fontSize: 13,
  cursorBlink: true,
  scrollback: 5000,
  theme: themes.dark,
});
const fit = new FitAddon.FitAddon();
terminal.loadAddon(fit);
terminal.open(element);

const post = (message) => window.chrome.webview.postMessage(message);
const decode = (base64) => Uint8Array.from(atob(base64), (character) => character.charCodeAt(0));

terminal.onData((data) => post({ type: 'input', data }));
terminal.onResize(({ cols, rows }) => post({ type: 'resize', columns: cols, rows }));
new ResizeObserver(() => fit.fit()).observe(element);

window.chrome.webview.addEventListener('message', (event) => {
  const message = event.data;
  switch (message.type) {
    case 'output':
      terminal.write(decode(message.data));
      break;
    case 'clear':
      terminal.reset();
      break;
    case 'theme':
      terminal.options.theme = themes[message.theme] ?? themes.dark;
      document.body.style.setProperty('--background', terminal.options.theme.background);
      break;
    case 'copy':
      post({ type: 'copied', text: terminal.getSelection() });
      break;
    case 'focus':
      terminal.focus();
      break;
  }
});

fit.fit();
post({ type: 'ready', columns: terminal.cols, rows: terminal.rows });
