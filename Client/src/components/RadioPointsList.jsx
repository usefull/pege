import { useState, useEffect } from 'react';

import '../styles/radio-points-list.scss';

const RadioPointsList = ({list, current, isVisible, onSelect}) => {

    const [isOpen, setIsOpen] = useState(false);
    const [shouldRender, setShouldRender] = useState(false);
    const [filter, setFilter] = useState('');


    useEffect(() => {
        if (isVisible) {
            setTimeout(() => setShouldRender(true), 1);
            setTimeout(() => setIsOpen(true), 50);
        } else {
            setTimeout(() => setIsOpen(false), 1);
            setTimeout(() => setShouldRender(false), 500);
        }

    }, [isVisible]);

    return(<>
        {shouldRender && <div className={`radio-points-list ${isOpen ? 'open' : ''}`}>
            <div className="filter">
                <input type='text' placeholder="Start typing for search" value={filter} onChange={(e) => setFilter(e.target.value)}></input>
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" className='button' stroke="none" fill="currentColor" onClick={() => setFilter('')}>
                    <path d="M2 9A1 1 0 002 11H18A1 1 0 0018 9Z" transform="rotate(45, 10, 10)" />
                    <path d="M2 9A1 1 0 002 11H18A1 1 0 0018 9Z" transform="rotate(-45, 10, 10)" />
                </svg>
            </div>
            <div className="list">
                {Object.entries(list).filter(([, title]) => title.toLowerCase().includes(filter.toLowerCase())).map(([id, title]) => (
                    <div key={id} className={id === current ? 'current': ''} onClick={() => onSelect(id)}>{title}</div>
                ))}
            </div>
        </div>}
    </>);
};

export default RadioPointsList;