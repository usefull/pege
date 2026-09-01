
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { HashRouter } from 'react-router-dom'
import { registerSW } from 'virtual:pwa-register'

import themeColors from './styles/theme.module.scss';
import './styles/index.scss'

import App from './App.jsx'

const isTgUserAgent = /Telegram/i.test(navigator.userAgent);

const renderApp = () => {
  createRoot(document.getElementById('root')).render(
    <StrictMode>
      <HashRouter>
          <App />
      </HashRouter>
    </StrictMode>,
  );
};

if (isTgUserAgent) {
    const checkTelegramAPI = setInterval(() => {
        const tgWebApp = window.Telegram?.WebApp;

        if (tgWebApp && typeof tgWebApp.ready === 'function') {
            tgWebApp.ready();            
            if (typeof tgWebApp.expand === 'function') {
                tgWebApp.expand();
            }
            tgWebApp.setHeaderColor(themeColors.tgColorHeaderBkgLight);
            tgWebApp.setBottomBarColor(themeColors.tgColorHeaderBkgLight);
            clearInterval(checkTelegramAPI);
            renderApp();
        }
    }, 50);
} else {
    renderApp();
}

registerSW({ 
    immediate: true,
    onNeedRefresh() {
        if (!isTgUserAgent) {
            window.location.reload();
        }
    }
});
