# Third-party notices

Shiying's application source is MIT licensed. Third-party binaries retain their own licenses.

- **Microsoft WebView2 SDK 1.0.4258.31**: Microsoft license, reproduced in `licenses/LICENSE.txt`. Uses the separately installed Evergreen WebView2 Runtime. https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31
- **yt-dlp 2026.08.19**: The source project is Unlicense; the Windows executable contains GPLv3+ components and is distributed under GPLv3+. License information: https://github.com/yt-dlp/yt-dlp/blob/2026.08.19/LICENSE . Complete corresponding project source and binary dependency information: https://github.com/yt-dlp/yt-dlp/tree/2026.08.19 and https://github.com/yt-dlp/yt-dlp#license . Official release: https://github.com/yt-dlp/yt-dlp/releases/tag/2026.08.19 . Original executable is redistributed without modification and verified by the release's SHA-256 checksum.
- **React / React DOM 19.3.0**, **Vite** (build-time): MIT. https://github.com/facebook/react and https://github.com/vitejs/vite
- **Lucide icons**: ISC. https://github.com/lucide-icons/lucide
- **QuickJS-ng 0.17.0**: MIT. Official executable: https://github.com/quickjs-ng/quickjs/releases/tag/v0.17.0 . Source and license: https://github.com/quickjs-ng/quickjs/tree/v0.17.0 . The upstream SHA-256 digest is verified before packaging. Used as yt-dlp's lightweight JavaScript runtime.
- **imageio-ffmpeg 0.6.0 / FFmpeg 7.1** (optional download): imageio-ffmpeg BSD-2-Clause; its FFmpeg executable retains FFmpeg's applicable GPL license. https://github.com/imageio/imageio-ffmpeg/tree/v0.6.0 and https://ffmpeg.org/legal.html . Source/build information: https://github.com/imageio/imageio-ffmpeg/tree/v0.6.0/binaries . Official PyPI wheel SHA-256 is verified before extraction.

Optional components are fetched directly from their published package registries. No user video, cookie file, or download history is uploaded to a Shiying server. Network requests to the original video platform and its media CDN are needed to resolve and download each video.
