import { useImperativeHandle, useState } from 'react';

import '../styles/main-button.scss'

const MainButton = ({onClick, title, ref}) => {

    const [on, setOn] = useState(false);

    const toggle = () => {
        const newState = !on;
        setOn(newState);
        if (onClick) onClick(newState);
    };

    useImperativeHandle(ref, () => ({
        click: () => toggle()
    }));    
    
    return (<div title={title} className={`main-button ${on ? 'on' : 'off'}`} >
        <div className='outer'></div>
        <div className='wave'></div>
        <svg fill="currentColor" fill-rule="evenodd" viewBox="0 0 47.8126 47.8126" onClick={toggle} xmlns="http://www.w3.org/2000/svg">
            <defs>
                <radialGradient id="grad" cx="50%" cy="50%" r="60%" >
                    <stop offset="0%" stop-color="var(--color1)" />
                    <stop offset="100%" stop-color="var(--color2)" />
                </radialGradient>
            </defs>
            <path fill="url(#grad)" d={on
                ? "M23.9062 47.8126C36.9609 47.8126 47.8126 36.9844 47.8126 23.9063 47.8126 10.8516 36.9375 0 23.8828 0 10.8046 0 0 10.8516 0 23.9063 0 36.9844 10.8281 47.8126 23.9062 47.8126ZM21 24V15A1 1 0 0020 14H16A1 1 0 0015 15V33A1 1 0 0016 34H20A1 1 0 0021 33ZM27 24V33A1 1 0 0028 34H32A1 1 0 0033 33V15A1 1 0 0032 14H28A1 1 0 0027 15Z"
                : "M23.9062 47.8126C36.9609 47.8126 47.8126 36.9844 47.8126 23.9063 47.8126 10.8516 36.9375 0 23.8828 0 10.8046 0 0 10.8516 0 23.9063 0 36.9844 10.8281 47.8126 23.9062 47.8126ZM19.6172 32.9532C18.539 33.6094 17.3203 33.0938 17.3203 31.9688L17.3203 15.8438C17.3203 14.7657 18.6093 14.2969 19.6172 14.8829L32.789 22.6875C33.7499 23.25 33.7734 24.586 32.789 25.1719Z"
            }/>
        </svg>
    </div>);
};

export default MainButton;