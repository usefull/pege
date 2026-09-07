import { useRef } from 'react';
import { useNavigate } from 'react-router';

import MainButton from '../components/MainButton';
import FadeIn from '../components/FadeIn';
import ShiftButton from '../components/ShiftButton';

import '../styles/home.scss'

const SERVER_ORIGIN = import.meta.env.VITE_SERVER_ORIGIN === 'DYNAMIC' ? window.location.origin : import.meta.env.VITE_SERVER_ORIGIN;

const Home = () => {

    const navigate = useNavigate();
    const mainButtonRef = useRef(null);
    
    return (<FadeIn>
        <div className="home-container">
            <div className="control-panel">
                <ShiftButton dir='back' title="Prev stream"></ShiftButton>
                <MainButton ref={mainButtonRef} title="Play / Stop" onClick={() => console.log('1111')}></MainButton>
                <ShiftButton title="Next stream"></ShiftButton>
            </div>
            <div className="header-panel" onClick={() => mainButtonRef.current.click()}>header</div>
            <div className="footer-panel">footer</div>
        </div>
        <div className='svg-button list-button' title="Stream list" onClick={() => navigate(`/streams`)}>
            <svg viewBox="0 0 24 24" fill="currentColor" fill-rule="evenodd" clip-rule="evenodd" xmlns="http://www.w3.org/2000/svg">
                <path d="M5 7C5 6.44772 5.44772 6 6 6H18C18.5523 6 19 6.44772 19 7C19 7.55228 18.5523 8 18 8H6C5.44772 8 5 7.55228 5 7ZM5 12C5 11.4477 5.44772 11 6 11H18C18.5523 11 19 11.4477 19 12C19 12.5523 18.5523 13 18 13H6C5.44772 13 5 12.5523 5 12ZM5 17C5 16.4477 5.44772 16 6 16H18C18.5523 16 19 16.4477 19 17C19 17.5523 18.5523 18 18 18H6C5.44772 18 5 17.5523 5 17Z"/>
            </svg>
        </div>
        <div className='svg-button eqalizer-button' title="Equalizer">
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