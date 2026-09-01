import { useEffect, useRef } from 'react'

import '../styles/splash.scss'

const Splash = ({ isStarting, setIsReady }) => {

    const logo = useRef(null);
    const o = useRef(null);

    useEffect(() => {
        if (!isStarting) {  
            const onIteration = () => {
                logo.current.removeEventListener('animationiteration', onIteration);

                let animationsCompleted = 0;
                const onEnd = () => {
                    animationsCompleted++;
                    if (animationsCompleted > 1) {
                        o.current.removeEventListener('animationend', onEnd);
                        setIsReady(true);
                    }
                }
                logo.current.classList.add('collapsed');
                o.current.addEventListener('animationend', onEnd);
            };
            logo.current.addEventListener('animationiteration', onIteration);
        }
    }, [isStarting])

    return (
        <div className='splash'>
            <div className="logo-container">
                <svg ref={logo} xmlns="http://www.w3.org/2000/svg" viewBox="0 0 22 22" strokeWidth="1" strokeLinecap="round" stroke="currentColor" fill="none">
                    <path ref={o} d="M 2 13 A 1 1 0 0 0 10 13 A 1 1 0 0 0 2 13" />
                    <path d="M 12 13 A 1 1 0 0 0 20 13 V 9 A 1 1 0 0 0 12 9 Z" />
                </svg>
            </div>
        </div>
    )
};

export default Splash;