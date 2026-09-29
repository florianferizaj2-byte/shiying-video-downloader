let sequence = 0;
const pending = new Map();
const listeners = new Set();
const native = window.chrome?.webview;

if (native) native.addEventListener('message', ({ data }) => {
  if (data.type === 'progress') listeners.forEach(listener => listener(data.data));
  if (data.type === 'response') {
    const request = pending.get(data.id);
    if (!request) return;
    pending.delete(data.id);
    if (data.ok) request.resolve(data.data);
    else request.reject(Object.assign(new Error(data.error || '任务已取消'), { details: data.details, cancelled: data.cancelled }));
  }
});

export const isDesktop = !!native;
export function call(method, args = {}) {
  if (!native) {
    if (method === 'state') return Promise.resolve({ settings: { folder: '请在 Windows 桌面版中选择文件夹', proxy: '', cookieFile: '' }, history: [], components: { parser: false, merger: false, youtube: false }, portable: true, version: '0.1.0' });
    return Promise.reject(new Error('请打开 Windows 桌面版使用此功能，浏览器仅用于界面预览。'));
  }
  return new Promise((resolve, reject) => {
    const id = String(++sequence);
    pending.set(id, { resolve, reject });
    native.postMessage({ id, method, args });
  });
}
export function onProgress(listener) { listeners.add(listener); return () => listeners.delete(listener); }
