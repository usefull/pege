import { useState } from 'react';
import { useNavigate } from 'react-router';

import { ArrowToLeftSvg } from '../components/Svg';
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
                    <ArrowToLeftSvg />
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