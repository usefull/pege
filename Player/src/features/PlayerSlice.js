
import { useDispatch, useSelector } from 'react-redux';
import { useCallback } from 'react';
import { createSlice } from '@reduxjs/toolkit';

const playerSlice = createSlice({
    name: 'player',
    initialState: {
        currentStream: null,
        isPlaying: false,
        isBuffering: false,
        togglePlayRequestId: 0,
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
        },
        toggleRequest: (state) => {
            state.togglePlayRequestId += 1;
        }
    }
});

export const {
    setCurrentStream,
    setIsPlaying,
    setIsBuffering,
    toggleRequest
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

export function useTogglePlay() {
    const dispatch = useDispatch();
    return useCallback(
        () => dispatch(toggleRequest()),
        [dispatch]
    );
};

export const useCurrentStream = () => useSelector((s) => s.player.currentStream);
export const useIsPlaying = () => useSelector((s) => s.player.isPlaying);
export const useIsBuffering = () => useSelector((s) => s.player.isBuffering);
export const useTogglePlayRequestId = () => useSelector((s) => s.player.togglePlayRequestId);
