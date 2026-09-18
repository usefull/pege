import { useNavigate } from 'react-router';

import { useSetEqOn, useSetEqGrains, useEqOn, useEqGrains } from '../features/EqualizerSlice';

import { ArrowToLeftSvg } from '../components/Svg';
import FadeIn from '../components/FadeIn';
import ToggleSwitch from '../components/ToggleSwitch';

import { CENTRAL_FREQS } from '../const';

import "../styles/equalizer.scss";

const Equalizer = () => {

    const navigate = useNavigate();
    const eqGrains = useEqGrains();
    const eqOn = useEqOn();
    const setEqGrains = useSetEqGrains();
    const setEqOn = useSetEqOn();

    const centralFreqToString = cf => cf >= 1000 ? `${cf / 1000}kHz` : `${cf}Hz`;

    return (<FadeIn>
        <div className='equalizer-container'>
            <div className='page-header'>
                <div className='svg-button go-back' title="Go back" onClick={() => navigate(`/`)}>
                    <ArrowToLeftSvg />
                </div>
                <span className='title'>Equalizer</span>
                <div className='toggle-switch-container'>
                    <ToggleSwitch checked={eqOn} onChange={e => setEqOn(e.target.checked)}></ToggleSwitch>
                </div>
            </div>
            <div className='grains'>{CENTRAL_FREQS.map((cf, i) =>
                <div key={i} className='eq-grain'>
                    <div className='hz-label'>
                        <span>{centralFreqToString(cf)}</span>
                        <div className='min-cutoff'></div>
                    </div>
                    <input type="range" min={-12} max={12} step={0.5} value={eqGrains ? eqGrains[i] : 0} onChange={e => {
                        console.log(eqGrains);
                        const grains = [...eqGrains];
                        grains[i] = e.target.value;
                        setEqGrains(grains);
                    }}/>
                    <div className='hz-label'>
                        <span>{centralFreqToString(cf)}</span>
                        <div className='max-cutoff'></div>
                    </div>
                    <div className='zero-cutoff'></div>
                </div>
            )}</div>
        </div>
    </FadeIn>);

};

export default Equalizer;