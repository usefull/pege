import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router';

import { useStreams } from '../features/StreamsSlice';
import { useCurrentStream, useSetCurrentStream, useIsBuffering, useTogglePlay, useMetadata } from "../features/PlayerSlice";

import { ListSvg, EqualizerSvg, FlawSvg, InfoSvg } from '../components/Svg';
import MainButton from '../components/MainButton';
import FadeIn from '../components/FadeIn';
import ShiftButton from '../components/ShiftButton';
import MarqueeText from '../components/MarqueeText';
import Modal from '../components/Modal';

import '../styles/home.scss'
import NoWrap from '../components/NoWrap';

const Home = () => {

    const navigate = useNavigate();
    const streams = useStreams();
    const currentStream = useCurrentStream();
    const setCurrentStream = useSetCurrentStream();
    const isBuffering = useIsBuffering();
    const togglePlay = useTogglePlay();
    const metadata = useMetadata();

    const [currentStreamInfo, setCurrentStreamInfo] = useState(null);
    const [flawInfoVisible, setFlawInfoVisible] = useState(false);

    useEffect(() => {setTimeout(() => setCurrentStreamInfo(streams.items.find(s => s.id === currentStream)), 1)}, [currentStream]);

    const setNextStream = (forward) => {
        if (!streams.items || streams.items.length === 0) return;
        let index = streams.items.findIndex(s => s.id === currentStream);
        if (index < 0)
            index = 0;
        else if (forward) {
            index++;
            if (index >= streams.items.length)
                index = 0;
        } else {
            index--;
            if (index < 0)
                index = streams.items.length - 1;
        }
        setCurrentStream(streams.items[index].id);
    }
    
    return (<FadeIn>
        <div className="home-container">
            <div className="control-panel">
                <ShiftButton dir='back' title="Prev stream" onClick={() => setNextStream(false)}></ShiftButton>
                <MainButton title="Play / Stop" onClick={() => togglePlay()}></MainButton>
                <ShiftButton title="Next stream" onClick={() => setNextStream(true)}></ShiftButton>
            </div>
            <div className="header-panel">
                <MarqueeText>{currentStreamInfo ? currentStreamInfo.title : ''}</MarqueeText>
                <NoWrap>
                    <span>{currentStreamInfo ? currentStreamInfo.country : ''}</span>
                    <InfoSvg onClick={() => navigate(`/info`)} />
                </NoWrap>
            </div>
            <div className="footer-panel">
                <MarqueeText>{isBuffering ? '' : <>{metadata ? metadata.track : ''}{metadata && metadata.fromFlac === false && <FlawSvg onClick={() => setFlawInfoVisible(true)} />}</>}</MarqueeText>
                <MarqueeText>{isBuffering ? <span className='buffering'></span> : <>{metadata ? metadata.artist : ''}</>}</MarqueeText>
                <MarqueeText>{isBuffering ? '' : <>{metadata && metadata.next && <><u>Next up</u>:&nbsp;{metadata.next}</>}</>}</MarqueeText>
            </div>
        </div>
        <div className='svg-button list-button' title="Stream list" onClick={() => navigate(`/streams`)}>
            <ListSvg />
        </div>
        <div className='svg-button eqalizer-button' title="Equalizer" onClick={() => navigate(`/eq`)}>
            <EqualizerSvg />
        </div>
        <Modal visible={flawInfoVisible} onClose={() => setFlawInfoVisible(false)}>
            <p>Heads up: this track — and every other track marked with <FlawSvg /> icon — was prepared from a lossy source file. The majority of our stream, however, comes directly from true FLAC.</p>
        </Modal>
    </FadeIn>);
};

export default Home;