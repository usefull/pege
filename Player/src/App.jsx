import { lazy, useEffect, useState } from 'react'
import { Routes, Route } from 'react-router-dom'

import { useStreams, useUploadStreams } from './features/StreamsSlice';
import { useSetCurrentStream, useCurrentStream } from './features/PlayerSlice';

const Home = lazy(() => import('./pages/Home'))
const StreamList = lazy(() => import('./pages/StreamList'))
const Equalizer = lazy(() => import('./pages/Equalizer'))
const Info = lazy(() => import('./pages/Info'))
const Splash = lazy(() => import('./components/Splash'))

const App = () => {

    const uploadStreams = useUploadStreams();
    const setCurrentStream = useSetCurrentStream();
    const streams = useStreams();
    const currentStream = useCurrentStream();

    const [isStarting, setIsStarting] = useState(true);
    const [isReady, setIsReady] = useState(false);

    useEffect(() => {
        uploadStreams();
    }, [uploadStreams]);

    useEffect(() => {
        if (streams.status === 'succeeded' && streams.items) {
            if (!streams.items || streams.items.length === 0 || streams.error)
                setCurrentStream(null);
            else if (!currentStream || streams.items.findIndex(s => s.id === currentStream) < 0) {
                setCurrentStream(streams.items[0].id);
            }

            setTimeout(() => setIsStarting(false), 1);
        }
    }, [streams]);

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