import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { I18nextProvider, useTranslation } from 'react-i18next';
import {  BuildocsProvider, LanguageSwitcher} from '@buildocsdev/sdk';
import { Form } from '@buildocsdev/sdk/form';

import '@buildocsdev/sdk/form.css';
import '@buildocsdev/sdk/formcanvas.css';
import '@buildocsdev/sdk/providers/theme.css';
import { FormHostProvider } from './provider/formhostprovider';
import i18n from './i18n';

function App() {
  const { i18n: { language } } = useTranslation();
  return (
    <I18nextProvider i18n={i18n}>
      <BrowserRouter>
        <BuildocsProvider apiKey="5cdb2058-6227-49b0-af6e-8c6a42c3d448">
          <FormHostProvider>
              <div style={{ paddingBottom: '5rem', display: 'flex', justifyContent: 'flex-end', padding: '16px' }}>
                <LanguageSwitcher />
              </div>
              <div>
                <Form
                  key={language}
                  params={{
                    formCode: "EMPLTBL",
                    guid: "new",
                    pluginCode: "INTRA",
                    pGuid: "",
                  }}
                  enableEventHandlers={true}
                />
              </div>
          </FormHostProvider>
        </BuildocsProvider>
      </BrowserRouter>
    </I18nextProvider>
  );
}

ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
    <App />
);

