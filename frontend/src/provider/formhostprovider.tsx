import React, { useEffect, useMemo, useState } from 'react'; // useEffect kept for token fetch

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

  const myApiUrl = 'http://localhost:7000/api/demo';
  const myApi = useBuildocsApi(myApiUrl, token);

  useEffect(() => {
    fetch(`${myApiUrl}/token`, { method: 'POST' })
      .then(r => r.json())
      .then((d: { token: string }) => setToken(d.token))
      .catch(e => console.error('Failed to fetch token:', e));
  }, []);

  const hostBridge = useMemo<HostBridge>(() => ({
    appSettings: {
      TOASTER_AUTOCLOSE: 1000,
      MAX_DOWNLOAD_NOCHUNK_FILESIZE: 10 * 1024 * 1024,
      SCREEN_WIDTH_TABLET: 767,
    },
    viewPort: { isMobileDevice, width, height },
    runFormEvent: async (args: RunEventArgs) => myApi.runEvent(args, '/runevent'),
    runFormUploadFiles: async (args: UploadArgs) => myApi.uploadFiles(args, '/uploadfiles'),
    runFormDeleteFileEvent: async (args: DeleteFileEventArgs) => myApi.deleteFile(args, '/deletefile'),
    runFormDownloadFileEvent: async (args: DownloadFileEventArgs) => myApi.downloadFile(args, '/downloadfile'),
    runFormGetLinkedFileEvent: async (args: GetLinkedFileMetaEventArgs) => myApi.getLinkedFile(args, '/getlinkedfile'),
    runFormGetLinkedFileMetaEvent: async (args: GetLinkedFileMetaEventArgs) => myApi.getLinkedFileMetaEvent(args, '/getlinkedfilemeta'),
    runFormGetLinkedFileByChunks: async (args: ChunkRequestArgs) => myApi.getLinkedFileByChunks(args, '/getlinkedfilebychunks'),
    runFormGetPresignedUrl: async (args: FileMetaEventArgs) => myApi.getPresignedUrl(args, '/getpresignedurl'),
    runLoadFormEvent: async (args: TLoadForm) => {
        const buildocsApiResponse = await buildocsApi.fetchFormDefinitionWithMeta(args.formCode);
        const result = await myApi.runLoadForm({...args, ...buildocsApiResponse?.payload}, '/loadform');
        return result;
    },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }), [isMobileDevice, width, height]);

  return (
    <BuildocsHostProvider hostBridge={hostBridge}>
      <div style={{ position: 'relative' }}>
        {children}
      </div>
    </BuildocsHostProvider>
  );
}