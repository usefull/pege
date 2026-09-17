import { lazy, useEffect, useState, useRef } from 'react';
import { Routes, Route } from 'react-router-dom';

import { SERVER_ORIGIN, CENTRAL_FREQS} from './const';

import { useStreams, useUploadStreams } from './features/StreamsSlice';
import { useSetCurrentStream, useCurrentStream, useSetIsPlaying, useSetIsBuffering } from './features/PlayerSlice';

import Splash from './components/Splash';
import RadioPlayer from './components/RadioPlayer';

const Home = lazy(() => import('./pages/Home'));
const StreamList = lazy(() => import('./pages/StreamList'));
const Equalizer = lazy(() => import('./pages/Equalizer'));
const Info = lazy(() => import('./pages/Info'));

const App = () => {

    const uploadStreams = useUploadStreams();
    const setCurrentStream = useSetCurrentStream();
    const setIsPlaying = useSetIsPlaying();
    const setIsBuffering = useSetIsBuffering();
    const streams = useStreams();
    const currentStream = useCurrentStream();

    const [isStarting, setIsStarting] = useState(true);
    const [isReady, setIsReady] = useState(false);

    const togglePlayFn = useRef(null);

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

    const handleToggleReady = (toggleFn) => togglePlayFn.current = toggleFn;

    return (!isReady ? <Splash isStarting={isStarting} setIsReady={setIsReady} /> : <>
        <RadioPlayer
            streamUrl={currentStream ? `${SERVER_ORIGIN}/stream/${currentStream}` : null}
            setIsPlaying={setIsPlaying}
            onToggleReady={handleToggleReady}
            onBuffering={setIsBuffering}
            equalizerOn={false}
            centralFreqs={CENTRAL_FREQS}
            eqGrains={[0,0,0,0,0,0,0,0,0]}
            onStreamInfoUpdate={info => {
                // if (info.Name) setStreamTitle(info.Name);
                // setStreamSubtitle(info.Country ? `(${info.Country})` : null);
                // setTrack(info.Track);
                // setArtist(info.Artist);
                // setFromFlac(info.FromFlac);
                // setStreamNext(info.Next)
            }}
        />
        <Routes>
            <Route path="/" element={<Home togglePlay={togglePlayFn} />} />
            <Route path="/streams" element={<StreamList togglePlay={togglePlayFn} />} />
            <Route path="/eq" element={<Equalizer />} />
            <Route path="/info" element={<Info />} />
        </Routes>
    </>)
};

export default App;