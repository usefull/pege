import { lazy } from 'react'
import { Routes, Route } from 'react-router-dom'

const Home = lazy(() => import('./pages/Home'))
const StreamList = lazy(() => import('./pages/StreamList'))
const Equalizer = lazy(() => import('./pages/Equalizer'))
//const Info = lazy(() => import('./pages/Info'))

function App() {
    return (<>
        <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/streams" element={<StreamList />} />
            <Route path="/eq" element={<Equalizer />} />
            {/* <Route path="/info" element={<Info />} /> */}
        </Routes>
    </>)
};

export default App;