
import { lazy } from 'react'
import { Routes, Route } from 'react-router-dom'

const Home = lazy(() => import('./pages/Home'))
const Admin = lazy(() => import('./pages/Admin'))

function App() {

    // Объект Telegram Web App
    //const [tg] = useState(window.Telegram?.WebApp);

    // Признак того, что приложение работает в Telegram
    //const [isTg] = useState(window.Telegram && window.Telegram.WebApp && window.Telegram.WebApp.initData && window.Telegram.WebApp.initData !== '');

    return (<>
        <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/adm" element={<Admin />} />
        </Routes>
    </>)
}

export default App
