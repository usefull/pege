import { lazy, useState, useEffect, useRef } from 'react';
import RadioPlayer from '../components/RadioPlayer';
import Equalizer from '../components/Equalizer';
import InfoPanel from '../components/InfoPanel';
import MarqueeText from '../components/MarqueeText';
import RadioPointsList from '../components/RadioPointsList';
import FlacLabel from '../components/FlacLabel';
import useLocalStorage from '../hooks/useLocalStorage';
import '../styles/home.scss';

const Splash = lazy(() => import('../components/Splash'));

const SERVER_ORIGIN = import.meta.env.VITE_SERVER_ORIGIN === 'DYNAMIC' ? window.location.origin : import.meta.env.VITE_SERVER_ORIGIN;

const CENTRAL_FREQS = [62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

const Home = () => {
    const [isStarting, setIsStarting] = useState(true);
    const [isReady, setIsReady] = useState(false);
    const [isPlaying, setIsPlaying] = useState(false);
    const [onBuffering, setOnBuffering] = useState(false);
    const [radioPoints, setRadioPoints] = useState({});
    const [currentRadioPoint, setCurrentRadioPoint] = useLocalStorage('currentRadioPoint', null);
    const togglePlayFn = useRef(null);
    const [streamTitle, setStreamTitle] = useState(null);
    const [streamSubtitle, setStreamSubtitle] = useState(null);
    const [streamNext, setStreamNext] = useState(null);
    const [track, setTrack] = useState(null);
    const [fromFlac, setFromFlac] = useState(false);
    const [artist, setArtist] = useState(null);
    const [showRadioPontList, setShowRadioPontList] = useState(false);
    const [showEqualizer, setShowEqualizer] = useState(false);
    const [showInfoPanel, setShowInfoPanel] = useState(false);
    const [equalizerOn, setEqualizerOn] = useLocalStorage('equalizerOn', false);
    const [eqGains, setEqGains] = useLocalStorage('eqGains', Array(CENTRAL_FREQS.length).fill(0));

    useEffect(() => {
        (async () => {
            try {
                const response = await fetch(SERVER_ORIGIN + '/api/stream/list');
                const result = await response.json();
                var defaultRadioPointId = null;
                var radioPoints = result.reduce((acc, rp) => {
                    if (!defaultRadioPointId)
                        defaultRadioPointId = rp.id;
                    acc[rp.id] = rp.title;
                    return acc;
                }, {});
                setRadioPoints(radioPoints);
                if (Object.keys(radioPoints).length === 0)
                    setCurrentRadioPoint(null);
                else if (!currentRadioPoint || !Object.hasOwn(radioPoints, currentRadioPoint))
                    setCurrentRadioPoint(defaultRadioPointId);
            } catch (error) {
                setCurrentRadioPoint(null);
            }
            finally {
                setIsStarting(false);
            }
        })();
    }, []);

    useEffect(() => {
        if (currentRadioPoint && radioPoints) {
            (async () => {
                setStreamTitle(radioPoints[currentRadioPoint]);
                setStreamSubtitle(null);
            })();
        }
    }, [currentRadioPoint, radioPoints]);

    // useEffect(() => {
    //     let intervalId;

    //     if (isPlaying) {
    //         const fetchData = async () => {
    //             try {
    //                 const response = await fetch(SERVER_ORIGIN + '/api/radio/status/' + currentRadioPoint);
    //                 const result = await response.json();
    //                 setStreamTitle(result.streamStatus.title);
    //                 setStreamSubtitle(result.streamStatus.subtitle);
    //                 setStreamNext(result.streamStatus.additional ? result.streamStatus.additional.Next : null);
    //             } catch (error) {
    //                 console.error('Ошибка:', error);
    //             }
    //         };

    //         // Выполняем запрос сразу при старте, затем периодически
    //         fetchData(); 
    //         intervalId = setInterval(fetchData, 5000); // Запрос каждые 5 секунд
    //     } else {
    //         setTimeout(() => {
    //             setStreamTitle(radioPoints[currentRadioPoint]);
    //             setStreamSubtitle(null);
    //         }, 10);
    //     }

    //     // Очистка таймера при остановке или размонтировании компонента
    //     return () => clearInterval(intervalId);
    // }, [isPlaying]);

    const handleToggleReady = (toggleFn) => togglePlayFn.current = toggleFn;

    const playClick = () => {   
        if (togglePlayFn.current) {
            console.log('Play click: equalizerOn is ' + equalizerOn);
            togglePlayFn.current();
        } else {
            console.error('togglePlayFn.current is null!');
        }
    };

    const switchRadioPoint = (forward) => {

        const points = Object.keys(radioPoints);

        let currentId = points.indexOf(currentRadioPoint);
        if (forward)
            currentId++;
        else
            currentId--;

        if(currentId < 0)
            currentId = points.length - 1;
        else if (currentId >= points.length)
            currentId = 0;

        const pointId = points[currentId];

        setCurrentRadioPoint(pointId);
    };

    const selectRadioPoint = (id) => {
        setCurrentRadioPoint(id);
        setShowRadioPontList(false);
        if (!isPlaying)
            playClick();
    };

    const toggleRadioPointList = () => {
        if (showRadioPontList)
            setShowRadioPontList(false);
        else {
            setShowRadioPontList(true);
            setShowEqualizer(false);
            setShowInfoPanel(false);
        }
    }

    const toggleEqualizerPanel = () => {
        if (showEqualizer)
            setShowEqualizer(false);
        else {
            setShowEqualizer(true);
            setShowRadioPontList(false);
            setShowInfoPanel(false);
        }
    }

    const toggleInfoPanel = () => {
        if (showInfoPanel)
            setShowInfoPanel(false);
        else {
            setShowInfoPanel(true);
            setShowEqualizer(false);
            setShowRadioPontList(false);
        }
    }

    return (<>
        <RadioPlayer
            streamUrl={currentRadioPoint ? SERVER_ORIGIN + '/stream/' + currentRadioPoint : null}
            setIsPlaying={setIsPlaying}
            onToggleReady={handleToggleReady}
            onBuffering={setOnBuffering}
            equalizerOn={equalizerOn}
            centralFreqs={CENTRAL_FREQS}
            eqGrains={eqGains}
            onStreamInfoUpdate={info => {
                console.log(info);
                if (info.Name) setStreamTitle(info.Name);
                setStreamSubtitle(info.Country ? `(${info.Country})` : null);
                setTrack(info.Track);
                setArtist(info.Artist);
                setFromFlac(info.FromFlac);
                setStreamNext(info.Next)
            }}
        />

        {(!isReady ? <Splash isStarting={isStarting} setIsReady={setIsReady} /> : <>
            
            <div className='header'>
                <div>
                    <svg xmlns="http://www.w3.org/2000/svg" className={`button${showRadioPontList ? ' close' : ''}`} viewBox="0 0 20 20" stroke="none" fill="currentColor" onClick={toggleRadioPointList}>
                        <path d="M1 4A1 1 0 001 6H19A1 1 0 0019 4Z" />
                        <path d="M1 9A1 1 0 001 11H19A1 1 0 0019 9Z" />
                        <path d="M1 14A1 1 0 001 16H19A1 1 0 0019 14Z" />
                    </svg>
                </div>
                <div>
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 89 16" strokeWidth="1.4" strokeLinecap="round" stroke="currentColor" fill="none">
                        <path d="M 2 10 A 1 1 0 0 0 10 10 A 1 1 0 0 0 2 10" />
                        <path d="M 12 10 A 1 1 0 0 0 20 10 V 6 A 1 1 0 0 0 12 6 Z" />
                        <path d="M 22 10 A 1 1 0 0 0 30 10 A 1 1 0 0 0 22 10" />
                        <path d="M 32 10 A 1 1 0 0 0 40 10 V 6 A 1 1 0 0 0 32 6 Z" />
                        <path d="M 42 14 A 0.1 0.1 0 0 0 42 13.8 A 0.1 0.1 0 0 0 42 14" />
                        <path d="M 44 10 A 1 1 0 0 0 52 10 A 1 1 0 0 0 44 10" />
                        <path d="M 54 14 V 6 H 59 A 2 2 0 0 1 61 8 V 14" />
                        <path d="M 64 14 V 2" />
                        <path d="M 67 14 V 8 M 67 5 A 0.1 0.1 0 0 0 67 4.8 A 0.1 0.1 0 0 0 67 5" />
                        <path d="M 70 14 V 6 H 75 A 2 2 0 0 1 77 8 V 14" />
                    <path d="M 79 10 H 87 A 1 1 0 0 0 79 10 A 4 4 0 0 0 86.5 12" />
                    </svg>
                </div>
                <div>
                    <svg xmlns="http://www.w3.org/2000/svg" className={`button equalizer${showEqualizer ? ' close' : ''}`} viewBox="0 0 20 20" stroke="none" fill="currentColor" onClick={toggleEqualizerPanel}>
                        <path d="M9 4A1 1 0 0111 4V16A1 1 0 019 16Z" />
                        <path d="M6 9A1 1 0 018 9V14A1 1 0 016 14Z" />
                        <path d="M3 8A1 1 0 015 8V12A1 1 0 013 12Z" />
                        <path d="M0 11A1 1 0 002 11 1 1 0 000 11" />
                        <path d="M12 11A1 1 0 0014 11V6A1 1 0 0012 6Z" />
                        <path d="M15 12A1 1 0 0017 12V8A1 1 0 0015 8Z" />
                        <path d="M18 10A1 1 0 0020 10 1 1 0 0018 10" />
                    </svg>
                    <svg xmlns="http://www.w3.org/2000/svg" className={`button info${showInfoPanel ? ' close' : ''}`} viewBox="0 0 20 20" stroke="none" fill="currentColor" onClick={toggleInfoPanel}>
                        <path d="M0 10A1 1 0 0020 10 1 1 0 000 10H2A1 1 0 0118 10 1 1 0 012 10" />
                        <circle cx="10" cy="6" r="1" />
                        <path d="M9 14A1 1 0 0011 14V10A1 1 0 009 10Z" />
                        <path d="M2.9289 15.6569A1 1 45 004.3431 17.0711L17.0711 4.3431A1 1 45 0015.6569 2.9289Z"/>
                    </svg>
                </div>
            </div>
            <div className="main-container">
                <div className='info-panel'>
                    <MarqueeText>{streamTitle}</MarqueeText>
                    <MarqueeText>{streamSubtitle}</MarqueeText>
                </div>
                <div className={'control-panel'}>
                    <div className='rewind'>
                    {isPlaying && <svg xmlns="http://www.w3.org/2000/svg" className='button' viewBox="0 0 7 12" strokeWidth={1.3} stroke="currentColor" fill="none" strokeLinejoin='round' strokeLinecap='round' onClick={() => switchRadioPoint(false)}>                            
                        <path d="M6 11 1 6 6 1" />
                    </svg>}
                    </div>
                    <div className={`play${isPlaying ? ' active' : ''}`}>
                    {currentRadioPoint && <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" className='button' strokeWidth={1.3} stroke="currentColor" fill="none" strokeLinejoin='round' onClick={playClick}>                            
                        <path d="M1 10A1 1 0 0019 10 1 1 0 001 10" />
                        <path id="run" d="M7.5 6V14L14.5 10Z" />
                        <path id="stop" d="M6 6V14H9V6ZM11 6V14H14V6Z" />
                    </svg>}
                    </div>
                    <div className='forward'>
                    {isPlaying && <svg xmlns="http://www.w3.org/2000/svg" className='button' viewBox="0 0 7 12" strokeWidth={1.3} stroke="currentColor" fill="none" strokeLinejoin='round' strokeLinecap='round' onClick={() => switchRadioPoint(true)}>                            
                        <path d="M1 1 6 6 1 11" />
                    </svg>}
                    </div>
                </div>
                <div className='ext-panel'>                
                    {onBuffering && <div className='buffering'></div>}
                    {isPlaying && !onBuffering && <>
                        <div className='track'><MarqueeText>
                            {track}
                            {fromFlac && <FlacLabel></FlacLabel>}
                        </MarqueeText></div>
                        <div className='artist'><MarqueeText>{artist}</MarqueeText></div>
                        {streamNext && <div className='next'><MarqueeText><u>Next:</u>&nbsp;{streamNext}</MarqueeText></div>}
                    </>}
                </div>
            </div>
            <div className='footer'>rev: {process.env.GIT_COMMIT} - {process.env.GIT_DATE}</div>
            <RadioPointsList list={radioPoints} current={currentRadioPoint} isVisible={showRadioPontList} onSelect={selectRadioPoint} />
            <Equalizer isVisible={showEqualizer} centralFreqs={CENTRAL_FREQS}
                equalizerOn={equalizerOn} setEqualizerOn={setEqualizerOn}
                eqGains={eqGains} setEqGains={setEqGains}
            />
            <InfoPanel isVisible={showInfoPanel} currentRadioPoint={currentRadioPoint} streamTitle={radioPoints[currentRadioPoint]} />
        </>)}
    </>);
};

export default Home;