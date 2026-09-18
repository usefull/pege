import { useDispatch, useSelector } from 'react-redux';
import { useCallback } from 'react';
import { createSlice } from '@reduxjs/toolkit';

import { CENTRAL_FREQS } from '../const';

const equalizerSlice = createSlice({
    name: 'equalizer',
    initialState: {
        eqOn: false,
        eqGrains: new Array(CENTRAL_FREQS.length).fill(0),
    },
    reducers: {
        setEqOn: (state, action) => {
            state.eqOn = action.payload;
        },
        setEqGrains: (state, action) => {
            state.eqGrains = action.payload;
        }
    }
});

export const {
    setEqOn,
    setEqGrains
} = equalizerSlice.actions;

export default equalizerSlice.reducer;

export function useSetEqOn() {
    const dispatch = useDispatch();
    return useCallback(
        (state) => dispatch(setEqOn(state)),
        [dispatch]
    );
};

export function useSetEqGrains() {
    const dispatch = useDispatch();
    return useCallback(
        (grains) => dispatch(setEqGrains(grains)),
        [dispatch]
    );
};

export const useEqOn = () => useSelector((s) => s.equalizer.eqOn);
export const useEqGrains = () => useSelector((s) => s.equalizer.eqGrains);