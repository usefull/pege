import { useNavigate } from 'react-router';

import MainButton from '../components/MainButton';
import FadeIn from '../components/FadeIn';
import ShiftButton from '../components/ShiftButton';
import MarqueeText from '../components/MarqueeText';

import '../styles/home.scss'
import NoWrap from '../components/NoWrap';

const SERVER_ORIGIN = import.meta.env.VITE_SERVER_ORIGIN === 'DYNAMIC' ? window.location.origin : import.meta.env.VITE_SERVER_ORIGIN;

const Home = () => {

    const navigate = useNavigate();
    
    return (<FadeIn>
        <div className="home-container">
            <div className="control-panel">
                <ShiftButton dir='back' title="Prev stream"></ShiftButton>
                <MainButton title="Play / Stop" onClick={() => console.log('1111')}></MainButton>
                <ShiftButton title="Next stream"></ShiftButton>
            </div>
            <div className="header-panel">
                <MarqueeText>WDR 2 Rheinland aktuell, Westdeutchscher Rundfunk Koeln</MarqueeText>
                <NoWrap>
                    <span>United Kingdom 12345 uytrgf b jfyrggtts 87534323</span>
                    <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
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
                <MarqueeText>Any Body Seen My Baby? (Remastered 2009)</MarqueeText>
                <MarqueeText>Rolling Stones & Depeche Mode</MarqueeText>
                <MarqueeText>
                    <u>Next</u>:&nbsp;"Livin' On The Edge" by Aerosmith
                </MarqueeText>
            </div>
        </div>
        <div className='svg-button list-button' title="Stream list" onClick={() => navigate(`/streams`)}>
            <svg viewBox="0 0 24 24" fill="currentColor" fillRule="evenodd" clipRule="evenodd" xmlns="http://www.w3.org/2000/svg">
                <path d="M5 7C5 6.44772 5.44772 6 6 6H18C18.5523 6 19 6.44772 19 7C19 7.55228 18.5523 8 18 8H6C5.44772 8 5 7.55228 5 7ZM5 12C5 11.4477 5.44772 11 6 11H18C18.5523 11 19 11.4477 19 12C19 12.5523 18.5523 13 18 13H6C5.44772 13 5 12.5523 5 12ZM5 17C5 16.4477 5.44772 16 6 16H18C18.5523 16 19 16.4477 19 17C19 17.5523 18.5523 18 18 18H6C5.44772 18 5 17.5523 5 17Z"/>
            </svg>
        </div>
        <div className='svg-button eqalizer-button' title="Equalizer" onClick={() => navigate(`/eq`)}>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" stroke="none" fill="currentColor">
                <path d="M9 4A1 1 0 0111 4V16A1 1 0 019 16Z" />
                <path d="M6 9A1 1 0 018 9V14A1 1 0 016 14Z" />
                <path d="M3 8A1 1 0 015 8V12A1 1 0 013 12Z" />
                <path d="M0 11A1 1 0 002 11 1 1 0 000 11" />
                <path d="M12 11A1 1 0 0014 11V6A1 1 0 0012 6Z" />
                <path d="M15 12A1 1 0 0017 12V8A1 1 0 0015 8Z" />
                <path d="M18 10A1 1 0 0020 10 1 1 0 0018 10" />
            </svg>
        </div>
    </FadeIn>);
};

export default Home;