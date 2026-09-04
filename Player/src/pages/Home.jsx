import { lazy, useState, useEffect, useRef } from 'react';

import MainButton from '../components/MainButton';

import '../styles/home.scss'
import ShiftButton from '../components/ShiftButton';

const SERVER_ORIGIN = import.meta.env.VITE_SERVER_ORIGIN === 'DYNAMIC' ? window.location.origin : import.meta.env.VITE_SERVER_ORIGIN;

const Home = () => {

    const mainButtonRef = useRef(null);
    
    return (<>
        <div className="home-container">
            <div className="control-panel">
                <ShiftButton dir='back' title="Prev stream"></ShiftButton>
                <MainButton ref={mainButtonRef} title="Play / Stop" onClick={() => console.log('1111')}></MainButton>
                <ShiftButton title="Next stream"></ShiftButton>
            </div>
            <div className="header-panel" onClick={() => mainButtonRef.current.click()}>header</div>
            <div className="footer-panel">footer</div>
        </div>
        <div className='svg-button list-button' title="Stream list">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 17 17" stroke="none" fill="currentColor">
                <path d="M1 3A1 1 0 001 5H16A1 1 0 0016 3Z" />
                <path d="M1 8A1 1 0 001 10H16A1 1 0 0016 8Z" />
                <path d="M1 13A1 1 0 001 15H16A1 1 0 0016 13Z" />
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
    </>);
};

export default Home;