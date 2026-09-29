import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { ArrowDownToLine, ArrowUpRight, Check, CheckCircle2, ChevronDown, Clipboard, Clock3, Download, FileVideo2, Film, Folder, FolderOpen, Info, Link2, LoaderCircle, Monitor, Play, Settings2, ShieldCheck, SlidersHorizontal, X } from 'lucide-react';
import { call, isDesktop, onProgress } from './bridge';
import './styles.css';

const formatBytes = n => !n ? '—' : n >= 1073741824 ? `${(n / 1073741824).toFixed(2)} GB` : n >= 1048576 ? `${(n / 1048576).toFixed(1)} MB` : `${Math.round(n / 1024)} KB`;
const duration = seconds => !seconds ? '时长未知' : `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;
const filename = path => path?.split(/[\\/]/).pop() || '';

function App() {
  const [app, setApp] = useState(null);
  const [input, setInput] = useState('');
  const [video, setVideo] = useState(null);
  const [quality, setQuality] = useState('best');
  const [busy, setBusy] = useState('');
  const [progress, setProgress] = useState({});
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');
  const [completed, setCompleted] = useState(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [proxy, setProxy] = useState('');
  const [brokenThumbnail, setBrokenThumbnail] = useState(false);
  const inputRef = useRef(null);
  const dialogRef = useRef(null);
  const settingsRef = useRef(null);
  const busyRef = useRef(false);

  useEffect(() => {
    call('state').then(state => { setApp(state); setProxy(state.settings.proxy); }).catch(setError);
    return onProgress(setProgress);
  }, []);
  useEffect(() => { setBrokenThumbnail(false); }, [video?.id]);
  useEffect(() => {
    if (settingsOpen) dialogRef.current?.showModal();
    else if (dialogRef.current?.open) { dialogRef.current.close(); settingsRef.current?.focus(); }
  }, [settingsOpen]);
  useEffect(() => { if (notice) { const timer = setTimeout(() => setNotice(''), 6000); return () => clearTimeout(timer); } }, [notice]);

  async function action(work) { try { await work(); } catch (err) { if (!err.cancelled) setError(err); } }
  async function analyze(event) {
    event?.preventDefault();
    if (busyRef.current || !input.trim() || !app) return;
    busyRef.current = true; setBusy('analyze'); setVideo(null); setCompleted(null); setError(null); setProgress({});
    try { const result = await call('analyze', { input }); setVideo(result); setQuality('best'); setApp(await call('state')); }
    catch (err) { if (!err.cancelled) setError(err); else setNotice('解析已取消'); }
    finally { busyRef.current = false; setBusy(''); }
  }
  async function download() {
    if (busyRef.current || !video) return;
    busyRef.current = true; setBusy('download'); setError(null); setCompleted(null); setProgress({});
    try { const result = await call('download', { id: video.id, quality }); setCompleted(result); setApp(await call('state')); }
    catch (err) { if (!err.cancelled) setError(err); else setNotice('下载已取消，已下载的片段会保留，重试时可继续。'); }
    finally { busyRef.current = false; setBusy(''); }
  }
  async function installComponent(name) {
    if (busyRef.current) return;
    busyRef.current = true; setBusy('component'); setError(null); setProgress({});
    try { const result = await call('component', { name }); setApp(result.state); if (result.video) { setVideo(result.video); setQuality('best'); } setNotice('组件已准备好'); }
    catch (err) { if (!err.cancelled) setError(err); else setNotice('组件下载已取消'); }
    finally { busyRef.current = false; setBusy(''); }
  }
  async function paste() {
    await action(async () => {
      const text = await call('clipboard');
      if (!text.trim()) { setNotice('剪贴板中没有文字，请先复制视频链接。'); return; }
      setInput(text.slice(0, 16000)); setVideo(null); setCompleted(null); setError(null); inputRef.current?.focus();
    });
  }
  const progressLabel = progress.message || (busy === 'analyze' ? '正在读取视频信息…' : busy === 'component' ? '正在准备组件…' : progress.stage === 'processing' ? '正在整理视频文件…' : '正在下载视频…');
  const percent = progress.percent;
  const showProgress = !!busy;
  const progressPanel = <div className="progress-panel" role="status" aria-live="polite">
    <div className="progress-heading"><span><LoaderCircle size={16} className="spinner" />{progressLabel}</span><button className="text-button" onClick={() => action(() => call('cancel'))}>取消</button></div>
    <div className={`progress-track ${percent == null ? 'indeterminate' : ''}`} role="progressbar" aria-label={progressLabel} aria-valuenow={percent == null ? undefined : Math.round(percent)} aria-valuemin={0} aria-valuemax={100}><span style={{ width: percent == null ? '35%' : `${percent}%` }} /></div>
    <div className="progress-meta"><span>{progress.downloaded ? `${formatBytes(progress.downloaded)}${progress.total ? ` / ${formatBytes(progress.total)}` : ''}` : '保持窗口打开，稍等一下'}</span><span>{progress.speed ? `${formatBytes(progress.speed)}/s` : ''}{percent != null ? `  ${Math.round(percent)}%` : ''}</span></div>
  </div>;
  const errorPanel = error && <div className="error-panel" role="alert"><Info size={18} /><div><p>{error.message}</p>{error.details && <details><summary>查看详细原因</summary><pre>{error.details}</pre></details>}</div><button className="icon-button small" aria-label="关闭错误提示" onClick={() => setError(null)}><X size={16} /></button></div>;

  return <div className="app-shell">
    <header className="topbar"><a className="brand" href="#main" aria-label="拾影主页"><span className="brand-mark"><Play size={19} fill="currentColor" /></span><span>拾影</span><span className="brand-divider" /><span className="brand-caption">把喜欢的视频，留下来</span></a>
      <button ref={settingsRef} className="quiet-button" onClick={() => { setError(null); setSettingsOpen(true); }}><Settings2 size={17} />连接设置</button></header>
    <main id="main">
      <div className="page-heading"><div><h1>一个链接，保存一段精彩。</h1><p>粘贴视频链接，选择位置，剩下的交给拾影。</p></div><span className="local-badge"><span />本地保存</span></div>
      <div className="workspace">
        <section className="main-panel" aria-label="解析视频">
          <form onSubmit={analyze} className="link-form">
            <div className="section-heading"><label htmlFor="video-url"><Link2 size={19} />视频链接</label><span className="step-label">01 / 解析</span></div>
            <div className="input-wrap"><textarea ref={inputRef} id="video-url" value={input} maxLength={16000} disabled={!!busy} spellCheck="false" placeholder={'在这里粘贴视频链接\n也可以直接粘贴整段分享文案'} onChange={e => { setInput(e.target.value); setVideo(null); setCompleted(null); setError(null); }} onKeyDown={e => { if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) analyze(e); }} aria-describedby="link-help" /><div className="input-tools"><span><Clipboard size={13} />支持分享文案自动提取</span><button type="button" className="paste-button" onClick={paste} disabled={!!busy}><Clipboard size={14} />粘贴链接</button></div></div>
            <div className="parse-row"><span id="link-help">一次解析一个视频</span><button className="primary-button parse-button" type="submit" disabled={!input.trim() || !!busy || !app}>{busy === 'analyze' ? <LoaderCircle size={17} className="spinner" /> : <Link2 size={17} />}{busy === 'analyze' ? '正在解析' : '解析视频'}</button></div>
          </form>
          <div className="video-area">
            {!video ? <div className="empty-state"><div className="film-illustration"><span className="film-hole hole-left" /><span className="film-hole hole-right" /><div className="film-screen"><Play size={28} strokeWidth={1.5} fill="currentColor" /></div><div className="link-orbit"><Link2 size={19} /></div></div><h2>{busy === 'analyze' ? '正在寻找这段精彩' : '你的视频，会出现在这里'}</h2><p>{busy === 'analyze' ? '正在获取封面、标题和可用画质' : '先复制一个视频链接，再点击「解析视频」'}</p><div className="platforms"><span>TikTok</span><span>抖音</span><span>快手</span><span className="youtube-brand"><Play size={11} fill="currentColor" />YouTube</span><span className="more-platforms">更多平台</span></div></div>
              : <article className="video-result"><div className="preview-frame">{video.thumbnail && !brokenThumbnail ? <img src={video.thumbnail} alt={video.title + '的视频封面'} onError={() => setBrokenThumbnail(true)} referrerPolicy="no-referrer" /> : <div className="thumbnail-fallback"><Film size={45} strokeWidth={1.3} /><span>视频信息已就绪</span></div>}<span className="source-tag">{video.platform}</span><span className="duration-tag">{duration(video.duration)}</span></div><div className="video-description"><span className="ready-label"><CheckCircle2 size={15} />解析完成</span><h2>{video.title}</h2><p>{video.uploader || '来自 ' + video.platform}</p></div></article>}
          </div>
          {!settingsOpen && errorPanel}
          {showProgress && !settingsOpen && progressPanel}
          {completed && <div className="success-panel" role="status"><CheckCircle2 size={22} /><div><strong>视频已保存</strong><span>{formatBytes(completed.size)} · {filename(completed.filePath)}</span></div><button className="text-button" onClick={() => action(() => call('reveal', { id: completed.id }))}>查看文件<ArrowUpRight size={15} /></button></div>}
        </section>
        <aside className="save-panel" aria-label="保存选项"><div className="section-heading"><h2><Folder size={19} />保存选项</h2><span className="step-label">02 / 下载</span></div>
          <div className="option-block"><div className="field-label"><label>保存到</label><button className="text-button" disabled={!!busy} onClick={() => action(async () => setApp(await call('folder')))}>更改</button></div><button className="folder-select" title={app?.settings.folder} disabled={!!busy || !app} onClick={() => action(async () => setApp(await call('folder')))}><FolderOpen size={24} /><span><strong>{filename(app?.settings.folder) || '加载保存位置…'}</strong><small>{app?.settings.folder || ' '}</small></span><ChevronDown size={16} /></button><p className="field-hint">自动记住上次选择的文件夹</p></div>
          <div className="option-block"><label htmlFor="quality">视频画质</label><div className="select-wrap"><Monitor size={18} /><select id="quality" value={quality} onChange={e => setQuality(e.target.value)} disabled={!video || !!busy}>{(video?.qualities || [{ value: 'best', label: '解析后选择画质' }]).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select><ChevronDown size={15} /></div><p className="field-hint">{app?.components.merger ? '优先保存为 MP4，保留原始画质' : '默认下载含声音的视频，高清合并可按需开启'}</p></div>
          {video?.needsMerger && <div className="merger-hint"><Info size={16} /><p>这个视频需要合并声音与画面。<button className="text-button" onClick={() => setSettingsOpen(true)}>准备高清组件</button></p></div>}
          <button className="primary-button download-button" disabled={!video || !!busy || video?.needsMerger} onClick={download}>{busy === 'download' ? <LoaderCircle className="spinner" size={18} /> : <ArrowDownToLine size={18} />}{busy === 'download' ? '正在保存视频' : '下载到文件夹'}</button>
          <button className="open-folder-button" disabled={!app} onClick={() => action(() => call('openFolder'))}><FolderOpen size={16} />打开保存文件夹<ArrowUpRight size={14} /></button>
          <div className="local-note"><ShieldCheck size={21} /><div><strong>下载直接保存在你的电脑</strong><p>无需账号，不经过中转服务器。</p></div></div>
        </aside>
      </div>
      <section className="history-section" aria-label="最近下载"><div className="history-header"><h2><Clock3 size={17} />最近下载</h2><span>{app?.history.length ? `保留最近 ${app.history.length} 条记录` : '已保存的视频会出现在这里'}</span></div>{app?.history.length ? <div className="history-list">{app.history.slice(0, 5).map(entry => <div className="history-row" key={entry.id}><span className="file-icon"><FileVideo2 size={21} /></span><div><strong title={entry.title}>{entry.title}</strong><p>{entry.platform}<span>{formatBytes(entry.size)}</span><span>{new Date(entry.completedAt).toLocaleDateString('zh-CN')}</span></p></div><span className="saved-badge"><Check size={14} />已保存</span><button className="icon-button" title="在文件夹中显示" aria-label={`在文件夹中显示 ${entry.title}`} onClick={() => action(() => call('reveal', { id: entry.id }))}><FolderOpen size={18} /></button></div>)}</div> : <div className="history-empty"><Download size={18} /><span>还没有下载记录，从第一个链接开始。</span></div>}</section>
      <footer><span>拾影 {app?.version || '0.1.0'}<span className="footer-divider">/</span>{app?.portable ? '便携版' : '安装版'}</span><span>{isDesktop ? '支持情况取决于平台和视频的访问权限' : '浏览器预览 · 下载功能请打开 Windows 桌面版'}</span></footer>
    </main>
    <dialog ref={dialogRef} onCancel={e => { e.preventDefault(); setSettingsOpen(false); }} className="settings-dialog" aria-labelledby="settings-title"><div className="dialog-heading"><div><h2 id="settings-title"><SlidersHorizontal size={20} />连接设置</h2><p>仅在无法访问或需要更高画质时调整。</p></div><button className="icon-button" aria-label="关闭连接设置" onClick={() => setSettingsOpen(false)}><X size={20} /></button></div>
      <div className="dialog-body"><label htmlFor="proxy">网络代理（可选）</label><input id="proxy" value={proxy} disabled={!!busy} onChange={e => setProxy(e.target.value)} placeholder="http://127.0.0.1:7890" /><p className="field-hint">访问海外视频平台时，可使用你自己的 HTTP 代理。</p><button className="secondary-button" disabled={!!busy} onClick={() => action(async () => { setApp(await call('settings', { proxy })); setNotice('连接设置已保存'); })}>保存代理设置</button>
        <div className="setting-section"><div className="field-label"><label>登录信息（可选）</label>{app?.settings.cookieFile && <button className="text-button" disabled={!!busy} onClick={() => action(async () => setApp(await call('clearCookies')))}>移除</button>}</div><p>部分平台要求已登录账号。选择浏览器导出的 Netscape 格式 cookies.txt，文件只在本机读取。</p><button className="secondary-button" disabled={!!busy} onClick={() => action(async () => setApp(await call('cookies')))}><FolderOpen size={16} />{app?.settings.cookieFile ? filename(app.settings.cookieFile) : '选择登录信息文件'}</button></div>
        <div className="setting-section"><label>按需组件</label><p>仅在需要时下载，不占用基础安装包空间。</p><div className="component-row"><span><strong>高清音画合并</strong><small>FFmpeg · 用于分离音轨和高清视频</small></span>{app?.components.merger ? <span className="ready-label"><CheckCircle2 size={15} />已就绪</span> : <button className="secondary-button compact" disabled={!!busy} onClick={() => installComponent('ffmpeg')}>下载组件</button>}</div><div className="component-row"><span><strong>YouTube 支持</strong><small>轻量支持组件 · 约 2 MB，随应用提供</small></span>{app?.components.youtube ? <span className="ready-label"><CheckCircle2 size={15} />已就绪</span> : <button className="secondary-button compact" disabled={!!busy} onClick={() => installComponent('qjs')}>提前准备</button>}</div></div>
        {settingsOpen && errorPanel}{settingsOpen && showProgress && progressPanel}
        <div className="settings-footnote"><Info size={15} /><span>便携版的设置和组件保存在应用旁的 Data 文件夹。安装版保存在当前用户目录。</span></div>
      </div></dialog>
    {notice && <div className="toast" role="status"><CheckCircle2 size={17} />{notice}</div>}
  </div>;
}

createRoot(document.getElementById('root')).render(<App />);
