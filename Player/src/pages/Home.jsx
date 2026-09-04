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
                <ShiftButton dir='back'></ShiftButton>
                <MainButton ref={mainButtonRef} onClick={() => console.log('1111')}></MainButton>
                <ShiftButton ></ShiftButton>
            </div>
            <div className="header-panel" onClick={() => mainButtonRef.current.click()}>header</div>
            <div className="footer-panel">footer</div>
        </div>
    </>);
};

export default Home;