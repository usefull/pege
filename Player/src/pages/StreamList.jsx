import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router';

import { useStreams, useUploadStreams } from '../features/StreamsSlice';
import { useCurrentStream, useSetCurrentStream, useIsPlaying, useTogglePlay } from "../features/PlayerSlice";

import { OnAirSvg, ArrowToLeftSvg, CrossSvg } from '../components/Svg';

import FadeIn from '../components/FadeIn';

import '../styles/stream-list.scss';

const StreamList = () => {

    const navigate = useNavigate();
    const streams = useStreams();
    const currentStream = useCurrentStream();
    const setCurrentStream = useSetCurrentStream();
    const uploadStreams = useUploadStreams();
    const isPlaying = useIsPlaying();
    const togglePlay = useTogglePlay();
    const selectRef = useRef(null);

    const [filter, setFilter] = useState('');

    useEffect(() => {
        if (Date.now() - streams.when > 200000)
            uploadStreams();
        selectRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, []);

    return (<FadeIn>
        <div className='streams-container'>
            <div className='page-header'>
                <div className='svg-button go-back' title="Go back" onClick={() => navigate(`/`)}>
                    <ArrowToLeftSvg />
                </div>
                <span className='title'>Streams</span>
                <div className="filter-edit">
                    <input value={filter} placeholder='type to search' onChange={e => setFilter(e.target.value.trim())} />
                    <div className='svg-button' onClick={() => setFilter('')}>
                        <CrossSvg />
                    </div>
                </div>
            </div>
            <div className='list'>{!streams.items ? <></> : streams.items.filter(s => filter === '' ? true : s.title.toLowerCase().includes(filter.toLowerCase())).map((s, i) =>
                <div ref={s.id === currentStream ? selectRef : null} key={i} className={`item${s.id === currentStream ? ' select' : ''}`} onClick={() => {
                    setCurrentStream(s.id);
                    if (!isPlaying) togglePlay();
                    navigate(`/`);
                }}>
                    <div>{s.title}</div>
                    <div>{s.country}</div>
                    {s.started && <OnAirSvg />}
                </div>
            )}</div>
        </div>
    </FadeIn>);
}

export default StreamList;