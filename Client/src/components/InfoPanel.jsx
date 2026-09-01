import { useState, useEffect } from 'react';
import QRCode from 'react-qr-code';

import '../styles/info-panel.scss';
import FlacLabel from './FlacLabel';

const SERVER_ORIGIN = import.meta.env.VITE_SERVER_ORIGIN === 'DYNAMIC' ? window.location.origin : import.meta.env.VITE_SERVER_ORIGIN;

function formatTimeSpanString(timeStr) {
  let days = 0;
  let restStr = timeStr;

  if (timeStr.includes('.') && timeStr.indexOf('.') < timeStr.indexOf(':')) {
    const parts = timeStr.split('.');
    days = parseInt(parts[0], 10);
    restStr = parts[1];
  }

  const [hoursStr, minutesStr, secondsWithMs] = restStr.split(':');
  const hours = parseInt(hoursStr, 10);
  const minutes = parseInt(minutesStr, 10);
  const seconds = Math.floor(parseFloat(secondsWithMs));

  const components = [
    { value: days, singular: "day", plural: "days" },
    { value: hours, singular: "hour", plural: "hours" },
    { value: minutes, singular: "minute", plural: "minutes" },
    { value: seconds, singular: "second", plural: "seconds" }
  ];

  const parts = components
    .filter(x => x.value > 0)
    .map(x => `${x.value} ${x.value === 1 ? x.singular : x.plural}`);

  if (parts.length === 0) {
    return "0 seconds";
  }

  return parts.join(" ");
}

const InfoPanel = ({ isVisible, currentRadioPoint, streamTitle }) => {

    const [isOpen, setIsOpen] = useState(false);
    const [shouldRender, setShouldRender] = useState(false);
    const [currentTab, setCurrentTab] = useState('stream');
    const [isActive, setIsActive] = useState(false);
    const [streamedAt, setStreamedAt] = useState(null);
    const [streamCountry, setStreamCountry] = useState(null);
    const [streamContentType, setStreamContentType] = useState(null);
    const [listeners, setListeners] = useState(null);
    const [track, setTrack] = useState(null);
    const [artist, setArtist] = useState(null);
    const [fromFlac, setFromFlac] = useState(null);
    const [nextTrack, setNextTrack] = useState(null);
    const [totalTracks, setTotalTracks] = useState(null);
    const [totalDuration, setTotalDuration] = useState(null);

    const formatter = new Intl.DateTimeFormat('ru-RU', {
        dateStyle: 'short',
        timeStyle: 'medium'
    });

    useEffect(() => {
        if (isVisible && currentRadioPoint)
            (async () => {
                const response = await fetch(SERVER_ORIGIN + `/api/stream/status/${currentRadioPoint}`);
                const result = await response.json();
                console.log(result.consumers);
                setIsActive(response.ok);
                setListeners(result.consumers !== undefined ? result.consumers : null);
                setStreamCountry(result.country ? result.country : null);
                setStreamContentType(result.contentType ? result.contentType : null);
                setStreamedAt(result.started ? formatter.format(new Date(result.started)) : null);
                setTrack(result.track ? result.track : null);
                setArtist(result.artist ? result.artist : null);
                setFromFlac(result.fromFlac ? result.fromFlac : null);
                setNextTrack(result.nextTrack ? `"${result.nextTrack}"${result.nextArtist ? ` by ${result.nextArtist}` : ""}` : null);
                setTotalTracks(result.totalTracks ? result.totalTracks : null);
                setTotalDuration(result.totalDuration ? formatTimeSpanString(result.totalDuration) : null);
            })();
    }, [isVisible]);

    useEffect(() => {
        if (isVisible) {
            setTimeout(() => setShouldRender(true), 1);
            setTimeout(() => setIsOpen(true), 50);
        } else {
            setTimeout(() => setIsOpen(false), 1);
            setTimeout(() => setShouldRender(false), 500);
        }

    }, [isVisible]);

    return (<>
        {shouldRender && <div className={`info-p ${isOpen ? 'open' : ''}`}>
            {currentRadioPoint && <div className='tabs'>
                <div className={currentTab === 'stream' ? "current-tab" : ""} onClick={() => setCurrentTab('stream')}>
                    <svg viewBox="0 0 16 16" fill="currentColor">
                        <path d="m 1.988281 1.988281 v 1.011719 c 0.007813 0.546875 0.453125 0.984375 1 0.988281 c 0.003907 -0.003906 0.007813 -0.003906 0.011719 -0.003906 v 0.027344 c 4.972656 0 8.988281 4.015625 8.988281 8.988281 c 0.003907 0.546875 0.449219 0.988281 1 0.984375 h 0.011719 h 0.988281 v -0.984375 h -0.003906 c 0 -0.003906 0 -0.003906 0.003906 -0.007812 c -0.003906 -5.972657 -4.804687 -10.84375 -10.746093 -10.972657 c -0.078126 -0.019531 -0.160157 -0.03125 -0.242188 -0.03125 v -0.003906 z m 0 4 v 1.011719 c 0.007813 0.546875 0.453125 0.984375 1 0.988281 c 0.003907 -0.003906 0.007813 -0.003906 0.011719 -0.003906 v 0.015625 c 2.71875 0 4.914062 2.144531 4.996094 4.84375 c -0.007813 0.046875 -0.011719 0.09375 -0.011719 0.144531 c 0 0.550781 0.449219 1 1 1 c 0.007813 -0.003906 0.011719 -0.003906 0.015625 -0.003906 v 0.003906 h 0.984375 v -0.988281 h 0.015625 c 0 -3.792969 -3.046875 -6.898438 -6.820312 -6.992188 c 0 -0.003906 -0.003907 -0.003906 -0.003907 -0.003906 c -0.058593 -0.011718 -0.117187 -0.015625 -0.175781 -0.015625 v -0.003906 z m 2 4 c -1.105469 0 -2 0.894531 -2 2 c 0 1.101563 0.894531 2 2 2 c 1.101563 0 2 -0.898437 2 -2 c 0 -1.105469 -0.898437 -2 -2 -2 z m 0 0"/>
                    </svg>
                    <span>Stream</span>
                </div>
                <div className={currentTab === 'app' ? "current-tab" : ""} onClick={() => setCurrentTab('app')}>
                    <svg viewBox="0 0 24 24" fill="none">
                        <rect x="5" y="3" width="14" height="18" rx="3" stroke="currentColor" strokeWidth="2"/>
                        <path d="M16 3H14.3575C13.5255 3 12.765 3.47005 12.3929 4.21417V4.21417C12.231 4.53795 11.769 4.53795 11.6071 4.21417V4.21417C11.235 3.47005 10.4745 3 9.64251 3H8" stroke="currentColor" strokeWidth="2"/>
                        <path d="M15 11L11.25 15L9 13.1818" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                    </svg>
                    <span>App</span>
                </div>
            </div>}
            {currentTab === 'stream' && <div className='i-content'>
                <div>
                    <a href={`https://o0o0.online/stream/${currentRadioPoint}`}>{`https://o0o0.online/stream/${currentRadioPoint}`}</a>
                </div>
                <div>
                    <QRCode value={`https://o0o0.online/stream/${currentRadioPoint}`} size={256} bgColor="transparent" fgColor="currentColor" level="H" />
                </div>
                <div className="title">
                    {streamTitle}
                    {streamCountry && <div className="country">
                        ({streamCountry})
                    </div>}
                </div>                
                <div className='additional'>
                    <span>Active:</span><span>{isActive ? "yes" : "no"}</span>
                </div>
                {streamedAt && <div className='additional'>
                    <span>Streamed at:</span><span>{streamedAt}</span>
                </div>}
                {streamContentType && <div className='additional'>
                    <span>Content type:</span><span>{streamContentType}</span>
                </div>}
                {track && <div className='additional'>
                    <span>Track:</span><span>{track}{fromFlac && <FlacLabel></FlacLabel>}</span>
                </div>}
                {artist && <div className='additional'>
                    <span>Artist:</span><span>{artist}</span>
                </div>}
                {nextTrack && <div className='additional'>
                    <span>Next track:</span><span>{nextTrack}</span>
                </div>}
                {totalTracks && <div className='additional'>
                    <span>Total tracks:</span><span>{totalTracks}</span>
                </div>}
                {totalDuration && <div className='additional'>
                    <span>Total duration:</span><span>{totalDuration}</span>
                </div>}
                {listeners !== null && <div className='additional'>
                    <span>Listeners:</span><span>{listeners}</span>
                </div>}
            </div>}
            {currentTab === 'app' && <div className='i-content'>
                <div>
                    <a href='https://o0o0.online'>https://o0o0.online</a>
                </div>
                <div>
                    <QRCode value='https://o0o0.online' size={256} bgColor="transparent" fgColor="currentColor" level="H" />
                </div>
            </div>}
        </div>}
    </>);
};

export default InfoPanel;