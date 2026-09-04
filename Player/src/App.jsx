
import { lazy } from 'react'
import { Routes, Route } from 'react-router-dom'

const Home = lazy(() => import('./pages/Home'))

function App() {
    return (<>
        <Routes>
            <Route path="/" element={<Home />} />
        </Routes>
    </>)
}

export default App
