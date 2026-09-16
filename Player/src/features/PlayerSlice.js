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
        }
    }
});

export const {
  setCurrentStream
} = playerSlice.actions;

export default playerSlice.reducer;

export function useSetCurrentStream() {
    const dispatch = useDispatch();
    return useCallback(
        (stream) => dispatch(setCurrentStream(stream)),
        [dispatch]
    );
}

export const useCurrentStream = () => useSelector((s) => s.player.currentStream);
