import { useEffect, useRef } from 'react'

import '../styles/splash.scss'

const Splash = ({ isStarting, setIsReady }) => {

    const logo = useRef(null);

    useEffect(() => {
        if (!isStarting) {    
            const onEnd = () => {                
                logo.current.removeEventListener('animationend', onEnd);
                setIsReady(true);
            };
            logo.current.classList.add('hide');
            logo.current.addEventListener('animationend', onEnd);
        }
    }, [isStarting, setIsReady])

    return (
        <div className='splash'>
            <div className="logo-container">
                <svg ref={logo} xmlns="http://www.w3.org/2000/svg" viewBox="0 0 22 22" strokeWidth="1" strokeLinecap="round" stroke="currentColor" fill="none">
                    <path d="M 2 13 A 1 1 0 0 0 10 13 A 1 1 0 0 0 2 13" />
                    <path d="M 12 13 A 1 1 0 0 0 20 13 V 9 A 1 1 0 0 0 12 9 Z" />
                </svg>
            </div>
        </div>
    )
};

export default Splash;