import { useState, useEffect } from 'react';
import ToggleSwitch from './ToggleSwitch';

import '../styles/equalizer.scss';

const Equalizer = ({isVisible, equalizerOn, setEqualizerOn, centralFreqs, eqGains, setEqGains}) => {

    const [isOpen, setIsOpen] = useState(false);
    const [shouldRender, setShouldRender] = useState(false);

    useEffect(() => {
        if (isVisible) {
            setTimeout(() => setShouldRender(true), 1);
            setTimeout(() => setIsOpen(true), 50);
        } else {
            setTimeout(() => setIsOpen(false), 1);
            setTimeout(() => setShouldRender(false), 500);
        }

    }, [isVisible]);

    const onEqGrainChange = (value, index) => {
        setEqGains(centralFreqs.map((_, i) => i === index ? value : i >= eqGains.length ? 0 : eqGains[i]));
    }

    return(<>
        {shouldRender && <div className={`equalizer-panel ${isOpen ? 'open' : ''}`}>
            <ToggleSwitch label={equalizerOn ? 'equalizer is ON' : 'equalizer is OFF'} checked={equalizerOn} onChange={e => {
                if (setEqualizerOn)
                    setEqualizerOn(e.target.checked);
            }} />
            <div>
                <div>
                    <div>
                        <div>+12dB</div>
                        <div>0</div>
                        <div>-12dB</div>
                        <div></div>
                    </div>
                    {centralFreqs.map((freq, i) => (
                        <div key={freq} className='eq-grain'>
                            {/* <div className='max-level'><span>-</span><span>-</span></div> */}
                            <div className='zero-level'><span>—</span><span>—</span></div>
                            {/* <div className='min-level'><span>-</span><span>-</span></div> */}
                            <input type="range" className='vertical' min={-12} max={12} step={0.5} value={eqGains[i]}
                                onChange={e => onEqGrainChange(e.target.value, i)} />
                            <div className='label'>{freq / 1000 < 1.0 ? `${freq}Hz` : `${freq / 1000}kHz`}</div>
                        </div>
                    ))}
                </div>
                <div className='spatial-panel'>
                </div>
            </div>
        </div>}
    </>);
};

export default Equalizer;