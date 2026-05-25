import React, { useEffect, useMemo, useRef, useState } from 'react'; // useEffect kept for token fetch

import {
  type TLoadForm,
  type HostBridge,
  type RunEventArgs,
  type UploadArgs,
  ChunkRequestArgs,
  DeleteFileEventArgs,
  DownloadFileEventArgs,
  FileMetaEventArgs,
  GetLinkedFileMetaEventArgs,
  BuildocsHostProvider,
  useBuildocsApi,
  useViewport,
} from '@buildocsdev/sdk';

interface FormHostProviderProps {
  readonly children: React.ReactNode;
}

export function FormHostProvider({ children }: FormHostProviderProps) {
  const { isMobileDevice, width, height } = useViewport();
  const [token, setToken] = useState<string | undefined>(undefined);

  const buildocsApi = useBuildocsApi();
  const buildocsApiRef = useRef(buildocsApi);
  buildocsApiRef.current = buildocsApi;

  const myApiUrl = 'http://localhost:7000/api/demo';
  const myApi = useBuildocsApi(myApiUrl, token);
  const myApiRef = useRef(myApi);
  myApiRef.current = myApi;

  useEffect(() => {
    fetch(`${myApiUrl}/token`, { method: 'POST' })
      .then(r => r.json())
      .then((d: { token: string }) => setToken(d.token))
      .catch(e => console.error('Failed to fetch token:', e));
  }, []);

  // Tracks whether the current form load has completed. Blocks pointer events
  // on the form until runLoadFormEvent finishes so that the MUI DatePicker's
  // `format` prop is already set before the user can type — preventing the
  // section-reset that MUI triggers whenever `format` changes (useFieldState.js
  // line 243: useEffect(..., [format])).
  const [formReady, setFormReady] = useState(false);
  // Monotonic counter lets the latest load cancel stale setFormReady(true) calls
  // from a previous load that was superseded (e.g. on language switch).
  const loadIdRef = useRef(0);

  // hostBridge must be stable — recreating it causes BuildocsHostProvider to
  // remount the form, which resets mid-input state (e.g. date field). Refs
  // let the callbacks always use the latest API instance without changing the
  // bridge object reference. setFormReady and loadIdRef are also stable refs.
  const hostBridge = useMemo<HostBridge>(() => ({
    appSettings: {
      TOASTER_AUTOCLOSE: 1000,
      MAX_DOWNLOAD_NOCHUNK_FILESIZE: 10 * 1024 * 1024,
      SCREEN_WIDTH_TABLET: 767,
    },
    viewPort: { isMobileDevice, width, height },
    runFormEvent: async (args: RunEventArgs) => myApiRef.current.runEvent(args, '/runevent'),
    runFormUploadFiles: async (args: UploadArgs) => myApiRef.current.uploadFiles(args, '/uploadfiles'),
    runFormDeleteFileEvent: async (args: DeleteFileEventArgs) => myApiRef.current.deleteFile(args, '/deletefile'),
    runFormDownloadFileEvent: async (args: DownloadFileEventArgs) => myApiRef.current.downloadFile(args, '/downloadfile'),
    runFormGetLinkedFileEvent: async (args: GetLinkedFileMetaEventArgs) => myApiRef.current.getLinkedFile(args, '/getlinkedfile'),
    runFormGetLinkedFileMetaEvent: async (args: GetLinkedFileMetaEventArgs) => myApiRef.current.getLinkedFileMetaEvent(args, '/getlinkedfilemeta'),
    runFormGetLinkedFileByChunks: async (args: ChunkRequestArgs) => myApiRef.current.getLinkedFileByChunks(args, '/getlinkedfilebychunks'),
    runFormGetPresignedUrl: async (args: FileMetaEventArgs) => myApiRef.current.getPresignedUrl(args, '/getpresignedurl'),
    runLoadFormEvent: async (args: TLoadForm) => {
      const myLoadId = ++loadIdRef.current;
      setFormReady(false);
      try {
        const buildocsApiResponse = await buildocsApiRef.current.fetchFormDefinitionWithMeta(args.formCode);
        const result = await myApiRef.current.runLoadForm({...args, ...buildocsApiResponse?.payload}, '/loadform');
        return result;
      } finally {
        if (loadIdRef.current === myLoadId) {
          setFormReady(true);
        }
      }
    },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }), [isMobileDevice, width, height]);

  return (
    <BuildocsHostProvider hostBridge={hostBridge}>
      {/* The overlay blocks all pointer interaction until runLoadFormEvent resolves
          and the date field's format prop is stable. pointer-events:none on a
          parent does NOT block children (CSS pointer-events isn't inherited), so
          we use a z-index overlay instead — the topmost element wins hit-testing,
          so clicks never reach the form below. Without this, typing during the
          async load triggers MUI's format-change useEffect (useFieldState.js:243)
          which resets partially-typed date sections back to placeholder values. */}
      <div style={{ position: 'relative' }}>
        {!formReady && (
          <div style={{ position: 'absolute', inset: 0, zIndex: 9999 }} />
        )}
        {children}
      </div>
    </BuildocsHostProvider>
  );
}