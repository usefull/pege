import { useState } from 'react';

import '../styles/shift-button.scss'

const ShiftButton = ({ dir, title, onClick }) => {

    const [active, setActive] = useState(false);
    
    return (<div title={title} className={`shift-button ${dir === 'back' ? 'back' : 'forward'} ${active ? 'active' : ''}`} onClick={() => {
        setActive(true);
        setTimeout(() => setActive(false), 500);
        if (onClick) onClick();
    }}>
        <div className='bkg'></div>
        <svg viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg">
            <defs>
                <radialGradient id="shift-button-grad" cx="50%" cy="50%" r="60%" >
                    <stop offset="0%" stop-color="var(--color1)" />
                    <stop offset="100%" stop-color="var(--color2)" />
                </radialGradient>
            </defs>
            <path fill="url(#shift-button-grad)" fill-rule="evenodd" clip-rule="evenodd" d="M8.2929 13.7071C7.9024 13.3166 7.9024 12.6834 8.2929 12.2929L10.5858 10 8.2929 7.7071C7.9024 7.3166 7.9024 6.6834 8.2929 6.2929 8.6834 5.9024 9.3166 5.9024 9.7071 6.2929L12.4229 9.0087C12.9704 9.5562 12.9704 10.4438 12.4229 10.9913L9.7071 13.7071C9.3166 14.0976 8.6834 14.0976 8.2929 13.7071ZM5.2501.3878C6.5488.0992 8.1243 0 10 0 11.8757 0 13.4512.0992 14.7499.3878 16.06.679 17.1488 1.176 17.9864 2.0136 18.824 2.8512 19.321 3.94 19.6122 5.2501 19.9008 6.5488 20 8.1243 20 10 20 11.8757 19.9008 13.4512 19.6122 14.7499 19.321 16.06 18.824 17.1488 17.9864 17.9864 17.1488 18.824 16.06 19.321 14.7499 19.6122 13.4512 19.9008 11.8757 20 10 20 8.1243 20 6.5488 19.9008 5.2501 19.6122 3.94 19.321 2.8512 18.824 2.0136 17.9864 1.176 17.1488.679 16.06.3878 14.7499.0992 13.4512 0 11.8757 0 10 0 8.1243.0992 6.5488.3878 5.2501.679 3.94 1.176 2.8512 2.0136 2.0136 2.8512 1.176 3.94.679 5.2501.3878Z"/>
        </svg>
    </div>);
};

export default ShiftButton;