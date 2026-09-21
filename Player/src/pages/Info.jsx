import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router';
import { renderToStaticMarkup } from 'react-dom/server';

import { SERVER_ORIGIN } from '../const';

import { FlacSvg, OnAirSvg, ArrowToLeftSvg, OnAirQuarterSvg, MobileCheckSvg, LocationSvg, TgSvg, PlaySvg } from "../components/Svg";
import QRCodeStyling from "qr-code-styling";

import { useStreams, useGetStreamStatus } from '../features/StreamsSlice';
import { useCurrentStream } from "../features/PlayerSlice";

import FadeIn from '../components/FadeIn';

import '../styles/info.scss';
import styles from '../styles/theme.module.scss';

const urlStream = `${SERVER_ORIGIN}/stream/`;
const urlTg = "https://t.me/o0o0_radio";

const svgPlayString = `data:image/svg+xml;base64,${btoa(
    Array.from(
        new TextEncoder().encode(renderToStaticMarkup(<PlaySvg style={{ color: styles.tgColorText}} />)),
        byte => String.fromCharCode(byte)
    ).join('')
)}`;

const svgTgString = `data:image/svg+xml;base64,${btoa(
    Array.from(
        new TextEncoder().encode(renderToStaticMarkup(<TgSvg style={{ color: styles.tgColorText}} />)),
        byte => String.fromCharCode(byte)
    ).join('')
)}`;

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

const Info = () => {

    const currentStream = useCurrentStream();
    const streams = useStreams();
    const getStreamStatus = useGetStreamStatus();

    const navigate = useNavigate();
    const [tab, setTab] = useState('stream');
    const [streamInfo, setStreamInfo] = useState(null);

    const refStreamQr = useRef(null);
    const refAppQr = useRef(null);
    const refTgQr = useRef(null);

    const qrStream = new QRCodeStyling({
        width: 175,
        height: 175,
        data: `${urlStream}${currentStream}`,
        type: 'svg',
        dotsOptions: {
            color: 'currentColor',
            type: 'dots',
        },
        backgroundOptions: {
            color: 'transparent',
        }               
    });

    const qrApp = new QRCodeStyling({
        width: 175,
        height: 175,
        image: svgPlayString,
        data: SERVER_ORIGIN,
        type: 'svg',
        dotsOptions: {
            color: 'currentColor',
            type: 'dots',
        },
        backgroundOptions: {
            color: 'transparent',
        },
        imageOptions: {
            imageSize: 0.4
        }               
    });

    const qrTg = new QRCodeStyling({
        width: 175,
        height: 175,
        image: svgTgString,
        data: urlTg,
        type: 'svg',
        dotsOptions: {
            color: 'currentColor',
            type: 'dots',
        },
        backgroundOptions: {
            color: 'transparent',
        },
        imageOptions: {
            imageSize: 0.35
        }             
    });

    useEffect(() => {
        if (!currentStream) return;
        getStreamStatus(currentStream);
    }, []);

    useEffect(() => {
        if (!streams) return;
        (async () => {
            const streamInfo = streams.items.find(s => s.id === currentStream);
            if (!streamInfo) return;
            setStreamInfo(streamInfo);
        })();
    }, [streams]);

    useEffect(() => {
        qrStream.update({
            data: `${urlStream}${currentStream}`
        });
        qrApp.update({
            data: SERVER_ORIGIN
        });
        qrTg.update({
            data: urlTg
        });
        qrStream.append(refStreamQr.current);
        qrApp.append(refAppQr.current);
        qrTg.append(refTgQr.current);
    }, [currentStream, tab]);

    return (<FadeIn>
        <div className='info-container'>
            <div className='page-header'>
                <div className='svg-button go-back' title="Go back" onClick={() => navigate(`/`)}>
                    <ArrowToLeftSvg />
                </div>
                <span className='title'>Info</span>
                <div className='tab-container'>
                    <div className='tab-control'>
                        <div className={`tab ${tab === 'stream' ? 'selected' : ''}`} onClick={() => setTab('stream')}>
                            <OnAirQuarterSvg />
                            <span>Stream</span>
                        </div>
                        <div className={`tab ${tab === 'app' ? 'selected' : ''}`} onClick={() => setTab('app')}>
                            <MobileCheckSvg />
                            <span>App</span>
                        </div>
                    </div>
                </div>                
            </div>
            {tab === 'stream' && <div className='info-panel'>
                <div className='title'>{streamInfo ? streamInfo.title : ''}</div>
                <div className='basic'>
                    <div className='tile qr'><a ref={refStreamQr} href={`${urlStream}${currentStream}`} /></div>
                    <div className='tiles'>
                        {streamInfo && streamInfo.country && streamInfo.country.trim() !== '' && <div className='tile country'>
                            <LocationSvg />{streamInfo.country}
                        </div>}
                        {streamInfo && streamInfo.contentType && <div className='tile codec'>{streamInfo.contentType}</div>}
                        {streamInfo && streamInfo.consumers > 0 && <div className='tile track'>
                            <span>Listeners:&nbsp;</span>
                            <span>{streamInfo.consumers}</span>
                        </div>}
                        <div className='tile onair'>{streamInfo && streamInfo.started ? <>
                            <OnAirSvg />ON AIR<br/>since {new Date(streamInfo.started).toLocaleString("en-US", {year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit", hour12: false})}</> : <>OFF AIR</>}
                        </div>
                    </div>
                </div>
                <div className='extra'>
                    {streamInfo && streamInfo.track && <div className='tile track'>
                        <span>NOW PLAYING:</span>
                        <span>{`"${streamInfo.track}" by ${streamInfo.artist}`}{streamInfo && streamInfo.fromFlac && <FlacSvg />}</span>
                    </div>}
                    {streamInfo && streamInfo.nextTrack && <div className='tile track'>
                        <span>NEXT UP:</span>
                        <span>{`"${streamInfo.nextTrack}" by ${streamInfo.nextArtist}`}{streamInfo && streamInfo.nextFromFlac && <FlacSvg />}</span>
                    </div>}
                    {streamInfo && streamInfo.totalTracks > 0 && <div className='tile track'>
                        <span>PLAYLIST:</span>
                        <span>{streamInfo.totalTracks}&nbsp;tracks</span>
                        <span>{formatTimeSpanString(streamInfo.totalDuration)}</span>
                    </div>}
                </div>
            </div>}
            {tab === 'app' && <div className='info-panel app'>
                <div className='rev'>rev. {process.env.GIT_COMMIT} - {process.env.GIT_DATE}</div>
                <div className='basic'>
                    <div className='tile qr'><a ref={refAppQr} href={SERVER_ORIGIN} /></div>
                    <div className='tile qr'><a ref={refTgQr} href="https://t.me/o0o0_radio" /></div>
                </div>
            </div>}
        </div>
    </FadeIn>);
};

export default Info;