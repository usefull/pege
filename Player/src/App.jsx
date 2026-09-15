import { lazy, useEffect, useState } from 'react'
import { Routes, Route } from 'react-router-dom'

import { SERVER_ORIGIN } from './const';

const Home = lazy(() => import('./pages/Home'))
const StreamList = lazy(() => import('./pages/StreamList'))
const Equalizer = lazy(() => import('./pages/Equalizer'))
const Info = lazy(() => import('./pages/Info'))
const Splash = lazy(() => import('./components/Splash'))

const App = () => {

    const [isStarting, setIsStarting] = useState(true);
    const [isReady, setIsReady] = useState(false);

    useEffect(() => {
        (async () => {
            try {
                const response = await fetch(SERVER_ORIGIN + '/api/stream/list');
                const result = await response.json();
                console.log(result);
            } catch (error) {
                //setCurrentRadioPoint(null);
            }
            finally {
                setIsStarting(false);
            }
        })();
    }, []);

    return (!isReady ? <Splash isStarting={isStarting} setIsReady={setIsReady} /> :
        <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/streams" element={<StreamList />} />
            <Route path="/eq" element={<Equalizer />} />
            <Route path="/info" element={<Info />} />
        </Routes>
    )
};

export default App;