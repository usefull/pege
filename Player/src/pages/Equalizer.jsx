import { useState } from 'react';
import { useNavigate } from 'react-router';


import FadeIn from '../components/FadeIn';
import ToggleSwitch from '../components/ToggleSwitch';


import "../styles/equalizer.scss";

const Equalizer = () => {

    const navigate = useNavigate();
    const [equalizerOn, setEqualizerOn] = useState(false);

    return (<FadeIn>
        <div className='equalizer-container'>
            <div className='page-header'>
                <div className='svg-button go-back' title="Go back" onClick={() => navigate(`/`)}>
                    <svg viewBox="0 0 24 24" fill="currentColor" fillRule="evenodd" clipRule="evenodd" xmlns="http://www.w3.org/2000/svg">
                        <path d="M11.7071 4.29289C12.0976 4.68342 12.0976 5.31658 11.7071 5.70711L6.41421 11H20C20.5523 11 21 11.4477 21 12C21 12.5523 20.5523 13 20 13H6.41421L11.7071 18.2929C12.0976 18.6834 12.0976 19.3166 11.7071 19.7071C11.3166 20.0976 10.6834 20.0976 10.2929 19.7071L3.29289 12.7071C3.10536 12.5196 3 12.2652 3 12C3 11.7348 3.10536 11.4804 3.29289 11.2929L10.2929 4.29289C10.6834 3.90237 11.3166 3.90237 11.7071 4.29289Z"/>
                    </svg>
                </div>
                <span className='title'>Equalizer</span>
                <div className='toggle-switch-container'>
                    <ToggleSwitch checked={equalizerOn} onChange={e => setEqualizerOn(e.target.checked)}></ToggleSwitch>
                </div>
            </div>
            <div className='grains'>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>62Hz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5} onChange={e => console.log(e.target.value)}/>
                    <div className='hz-label'>
                        <span>62Hz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>125Hz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5}/>
                    <div className='hz-label'>
                        <span>125Hz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>250Hz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5}/>
                    <div className='hz-label'>
                        <span>250Hz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>500Hz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5}/>
                    <div className='hz-label'>
                        <span>500Hz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>1kHz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5}/>
                    <div className='hz-label'>
                        <span>1kHz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>2kHz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5}/>
                    <div className='hz-label'>
                        <span>2kHz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>4kHz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5}/>
                    <div className='hz-label'>
                        <span>4kHz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>8kHz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5}/>
                    <div className='hz-label'>
                        <span>8kHz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
                <div className='eq-grain'>
                    <div className='hz-label'>
                        <span>16kHz</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5}/>
                    <div className='hz-label'>
                        <span>16kHz</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
            </div>
        </div>
    </FadeIn>);

};

export default Equalizer;