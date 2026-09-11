import { useState } from 'react';
import { useNavigate } from 'react-router';

import FadeIn from '../components/FadeIn';

import '../styles/info.scss';

const Info = () => {

    const navigate = useNavigate();
    const [tab, setTab] = useState('stream');

    return (<FadeIn>
        <div className='info-container'>
            <div className='page-header'>
                <div className='svg-button go-back' title="Go back" onClick={() => navigate(`/`)}>
                    <svg viewBox="0 0 24 24" fill="currentColor" fillRule="evenodd" clipRule="evenodd" xmlns="http://www.w3.org/2000/svg">
                        <path d="M11.7071 4.29289C12.0976 4.68342 12.0976 5.31658 11.7071 5.70711L6.41421 11H20C20.5523 11 21 11.4477 21 12C21 12.5523 20.5523 13 20 13H6.41421L11.7071 18.2929C12.0976 18.6834 12.0976 19.3166 11.7071 19.7071C11.3166 20.0976 10.6834 20.0976 10.2929 19.7071L3.29289 12.7071C3.10536 12.5196 3 12.2652 3 12C3 11.7348 3.10536 11.4804 3.29289 11.2929L10.2929 4.29289C10.6834 3.90237 11.3166 3.90237 11.7071 4.29289Z"/>
                    </svg>
                </div>
                <span className='title'>Info</span>
                <div className='tab-container'>
                    <div className='tab-control'>
                        <div className={`tab ${tab === 'stream' ? 'selected' : ''}`} onClick={() => setTab('stream')}>
                            <svg viewBox="0 0 16 16" fill="currentColor">
                                <path d="m 1.988281 1.988281 v 1.011719 c 0.007813 0.546875 0.453125 0.984375 1 0.988281 c 0.003907 -0.003906 0.007813 -0.003906 0.011719 -0.003906 v 0.027344 c 4.972656 0 8.988281 4.015625 8.988281 8.988281 c 0.003907 0.546875 0.449219 0.988281 1 0.984375 h 0.011719 h 0.988281 v -0.984375 h -0.003906 c 0 -0.003906 0 -0.003906 0.003906 -0.007812 c -0.003906 -5.972657 -4.804687 -10.84375 -10.746093 -10.972657 c -0.078126 -0.019531 -0.160157 -0.03125 -0.242188 -0.03125 v -0.003906 z m 0 4 v 1.011719 c 0.007813 0.546875 0.453125 0.984375 1 0.988281 c 0.003907 -0.003906 0.007813 -0.003906 0.011719 -0.003906 v 0.015625 c 2.71875 0 4.914062 2.144531 4.996094 4.84375 c -0.007813 0.046875 -0.011719 0.09375 -0.011719 0.144531 c 0 0.550781 0.449219 1 1 1 c 0.007813 -0.003906 0.011719 -0.003906 0.015625 -0.003906 v 0.003906 h 0.984375 v -0.988281 h 0.015625 c 0 -3.792969 -3.046875 -6.898438 -6.820312 -6.992188 c 0 -0.003906 -0.003907 -0.003906 -0.003907 -0.003906 c -0.058593 -0.011718 -0.117187 -0.015625 -0.175781 -0.015625 v -0.003906 z m 2 4 c -1.105469 0 -2 0.894531 -2 2 c 0 1.101563 0.894531 2 2 2 c 1.101563 0 2 -0.898437 2 -2 c 0 -1.105469 -0.898437 -2 -2 -2 z m 0 0"/>
                            </svg>
                            <span>Stream</span>
                        </div>
                        <div className={`tab ${tab === 'app' ? 'selected' : ''}`} onClick={() => setTab('app')}>
                            <svg viewBox="0 0 24 24" fill="none">
                                <rect x="5" y="3" width="14" height="18" rx="3" stroke="currentColor" strokeWidth="2"/>
                                <path d="M16 3H14.3575C13.5255 3 12.765 3.47005 12.3929 4.21417V4.21417C12.231 4.53795 11.769 4.53795 11.6071 4.21417V4.21417C11.235 3.47005 10.4745 3 9.64251 3H8" stroke="currentColor" strokeWidth="2"/>
                                <path d="M15 11L11.25 15L9 13.1818" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                            </svg>
                            <span>App</span>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </FadeIn>);
};

export default Info;