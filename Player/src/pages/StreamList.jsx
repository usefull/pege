import { useState } from 'react';
import { useNavigate } from 'react-router';

import FadeIn from '../components/FadeIn';

import '../styles/stream-list.scss';

const StreamList = () => {

    const navigate = useNavigate();

    const [filter, setFilter] = useState('');

    return (<FadeIn>
        <div className='streams-container'>
            <div className='page-header'>
                <div className='svg-button go-back' title="Go back" onClick={() => navigate(`/`)}>
                    <svg viewBox="0 0 24 24" fill="currentColor" fillRule="evenodd" clipRule="evenodd" xmlns="http://www.w3.org/2000/svg">
                        <path d="M11.7071 4.29289C12.0976 4.68342 12.0976 5.31658 11.7071 5.70711L6.41421 11H20C20.5523 11 21 11.4477 21 12C21 12.5523 20.5523 13 20 13H6.41421L11.7071 18.2929C12.0976 18.6834 12.0976 19.3166 11.7071 19.7071C11.3166 20.0976 10.6834 20.0976 10.2929 19.7071L3.29289 12.7071C3.10536 12.5196 3 12.2652 3 12C3 11.7348 3.10536 11.4804 3.29289 11.2929L10.2929 4.29289C10.6834 3.90237 11.3166 3.90237 11.7071 4.29289Z"/>
                    </svg>
                </div>
                <span className='title'>Streams</span>
                <div className="filter-edit">
                    <input value={filter} placeholder='type to search' onChange={e => setFilter(e.target.value)} />
                    <div className='svg-button' onClick={() => setFilter('')}>
                        <svg viewBox="0 0 24 24" fillRule="evenodd" clipRule="evenodd" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
                            <path d="M5.29289 5.29289C5.68342 4.90237 6.31658 4.90237 6.70711 5.29289L12 10.5858L17.2929 5.29289C17.6834 4.90237 18.3166 4.90237 18.7071 5.29289C19.0976 5.68342 19.0976 6.31658 18.7071 6.70711L13.4142 12L18.7071 17.2929C19.0976 17.6834 19.0976 18.3166 18.7071 18.7071C18.3166 19.0976 17.6834 19.0976 17.2929 18.7071L12 13.4142L6.70711 18.7071C6.31658 19.0976 5.68342 19.0976 5.29289 18.7071C4.90237 18.3166 4.90237 17.6834 5.29289 17.2929L10.5858 12L5.29289 6.70711C4.90237 6.31658 4.90237 5.68342 5.29289 5.29289Z"/>
                        </svg>
                    </div>
                </div>
            </div>
            <div className='list'>
                <div className='item select'>
                    <div>o0o0.online</div>
                    <div>Russia</div>
                </div>
                <div className='item'>
                    <div>94,3 RS2</div>
                    <div>Germany</div>
                </div>
                <div className='item'>
                    <div>BBC Radio</div>
                    <div>United Kingdom</div>
                </div>
                <div className='item'>
                    <div>o0o0.online</div>
                    <div>Russia</div>
                </div>
                <div className='item'>
                    <div>94,3 RS2</div>
                    <div>Germany</div>
                </div>
                <div className='item'>
                    <div>BBC Radio</div>
                    <div>United Kingdom</div>
                </div>
                <div className='item'>
                    <div>o0o0.online</div>
                    <div>Russia</div>
                </div>
                <div className='item'>
                    <div>94,3 RS2</div>
                    <div>Germany</div>
                </div>
                <div className='item'>
                    <div>BBC Radio</div>
                    <div>United Kingdom</div>
                </div>
                <div className='item'>
                    <div>o0o0.online</div>
                    <div>Russia</div>
                </div>
                <div className='item'>
                    <div>94,3 RS2</div>
                    <div>Germany</div>
                </div>
                <div className='item'>
                    <div>BBC Radio</div>
                    <div>United Kingdom</div>
                </div>
                <div className='item'>
                    <div>o0o0.online</div>
                    <div>Russia</div>
                </div>
                <div className='item'>
                    <div>94,3 RS2</div>
                    <div>Germany</div>
                </div>
                <div className='item'>
                    <div>BBC Radio</div>
                    <div>United Kingdom</div>
                </div>
                <div className='item'>
                    <div>o0o0.online</div>
                    <div>Russia</div>
                </div>
                <div className='item'>
                    <div>94,3 RS2</div>
                    <div>Germany</div>
                </div>
                <div className='item'>
                    <div>o0o0.online</div>
                    <div>Russia</div>
                </div>
                <div className='item'>
                    <div>94,3 RS2</div>
                    <div>Germany</div>
                </div>
                <div className='item select'>
                    <div>BBC Radio</div>
                    <div>United Kingdom</div>
                </div>
                <div className='item'>
                    <div>o0o0.online</div>
                    <div>Russia</div>
                </div>
                <div className='item select'>
                    <div>94,3 RS2</div>
                    <div>Germany</div>
                </div>
            </div>
        </div>
    </FadeIn>);
}

export default StreamList;