import { useDispatch, useSelector } from 'react-redux';
import { useCallback } from 'react';
import { createSlice } from '@reduxjs/toolkit';

const playerSlice = createSlice({
    name: 'player',
    initialState: {
        currentStream: null,
        isPlaying: false,
        isBuffering: false,
        error: null,
    },
    reducers: {
        setCurrentStream: (state, action) => {
            state.currentStream = action.payload;
        },
        setIsPlaying: (state, action) => {
            state.isPlaying = action.payload;
        },
        setIsBuffering: (state, action) => {
            state.isBuffering = action.payload;
        }
    }
});

export const {
    setCurrentStream,
    setIsPlaying,
    setIsBuffering
} = playerSlice.actions;

export default playerSlice.reducer;

export function useSetCurrentStream() {
    const dispatch = useDispatch();
    return useCallback(
        (stream) => dispatch(setCurrentStream(stream)),
        [dispatch]
    );
};

export function useSetIsPlaying() {
    const dispatch = useDispatch();
    return useCallback(
        (isPlaying) => dispatch(setIsPlaying(isPlaying)),
        [dispatch]
    );
};

export function useSetIsBuffering() {
    const dispatch = useDispatch();
    return useCallback(
        (isBuffering) => dispatch(setIsBuffering(isBuffering)),
        [dispatch]
    );
};

export const useCurrentStream = () => useSelector((s) => s.player.currentStream);
export const useIsPlaying = () => useSelector((s) => s.player.isPlaying);
export const useIsBuffering = () => useSelector((s) => s.player.isBuffering);
