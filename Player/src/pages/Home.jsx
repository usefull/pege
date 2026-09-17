import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router';

import { useStreams } from '../features/StreamsSlice';
import { useCurrentStream, useSetCurrentStream } from "../features/PlayerSlice";

import { ListSvg, EqualizerSvg, FlacSvg} from '../components/Svg';
import MainButton from '../components/MainButton';
import FadeIn from '../components/FadeIn';
import ShiftButton from '../components/ShiftButton';
import MarqueeText from '../components/MarqueeText';

import '../styles/home.scss'
import NoWrap from '../components/NoWrap';

const Home = () => {

    const navigate = useNavigate();
    const streams = useStreams();
    const currentStream = useCurrentStream();
    const setCurrentStream = useSetCurrentStream();

    const [currentStreamInfo, setCurrentStreamInfo] = useState(null);

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
                <MainButton title="Play / Stop" onClick={() => console.log('1111')}></MainButton>
                <ShiftButton title="Next stream" onClick={() => setNextStream(true)}></ShiftButton>
            </div>
            <div className="header-panel">
                <MarqueeText>{currentStreamInfo ? currentStreamInfo.title : ''}</MarqueeText>
                <NoWrap>
                    <span>{currentStreamInfo ? currentStreamInfo.country : ''}</span>
                    <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg" onClick={() => navigate(`/info`)}>
                        <defs>
                            <radialGradient id="info-button-grad" cx="50%" cy="50%" r="60%" >
                                <stop offset="0%" stopColor="var(--color1)" />
                                <stop offset="100%" stopColor="var(--color2)" />
                            </radialGradient>
                        </defs>
                        <path fill="url(#info-button-grad)" fillRule="evenodd" clipRule="evenodd" d="M12 21C7.02944 21 3 16.9706 3 12C3 7.02944 7.02944 3 12 3C16.9706 3 21 7.02944 21 12C21 16.9706 16.9706 21 12 21ZM12 7C11.4477 7 11 7.44772 11 8C11 8.55228 11.4477 9 12 9H12.01C12.5623 9 13.01 8.55228 13.01 8C13.01 7.44772 12.5623 7 12.01 7H12ZM10.5 11C9.94772 11 9.5 11.4477 9.5 12C9.5 12.5523 9.94772 13 10.5 13H11V16C11 16.5523 11.4477 17 12 17H14C14.5523 17 15 16.5523 15 16C15 15.4477 14.5523 15 14 15H13V12C13 11.4477 12.5523 11 12 11H10.5Z"/>
                    </svg>
                </NoWrap>
            </div>
            <div className="footer-panel">
                <MarqueeText>Any Body Seen My Baby? (Remastered 2009)<FlacSvg /></MarqueeText>
                <MarqueeText>Rolling Stones & Depeche Mode</MarqueeText>
                <MarqueeText>
                    <u>Next up</u>:&nbsp;"Livin' On The Edge" by Aerosmith
                </MarqueeText>
            </div>
        </div>
        <div className='svg-button list-button' title="Stream list" onClick={() => navigate(`/streams`)}>
            <ListSvg />
        </div>
        <div className='svg-button eqalizer-button' title="Equalizer" onClick={() => navigate(`/eq`)}>
            <EqualizerSvg />
        </div>
    </FadeIn>);
};

export default Home;