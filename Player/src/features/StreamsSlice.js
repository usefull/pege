import { useDispatch, useSelector } from 'react-redux';
import { useCallback } from 'react';
import { createSlice, createAsyncThunk  } from '@reduxjs/toolkit';
import { SERVER_ORIGIN } from '../const';

const uploadStreamsThunk = createAsyncThunk(
    'streams/uploadStreams',
    async () => {
        const response = await fetch(SERVER_ORIGIN + '/api/stream/list');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return await response.json();
    }
);

export const useUploadStreams = () => {
    const dispatch = useDispatch();
    return useCallback(
        () => dispatch(uploadStreamsThunk()),
        [dispatch]
    );
};

export const useStreams = () => {
    const items = useSelector((s) => s.streams.items);
    const status = useSelector((s) => s.streams.status);
    const error = useSelector((s) => s.streams.error);
    const when = useSelector((s) => s.streams.when);
    return { items, status, error, when };
};

const streamsSlice = createSlice({
    name: 'streams',
    initialState: {
        items: [],
        status: 'idle', // 'idle' | 'loading' | 'succeeded' | 'failed'
        when: null,     // время, когда в последний раз был успешно загружен список
        error: null,
    },
    reducers: {},
    extraReducers: (builder) => {builder
        .addCase(uploadStreamsThunk.pending, (state) => {
            state.status = 'loading';
        })
        .addCase(uploadStreamsThunk.fulfilled, (state, action) => {
            state.status = 'succeeded';
            state.items = action.payload;
            state.when = Date.now()
        })
        .addCase(uploadStreamsThunk.rejected, (state, action) => {
            state.status = 'failed';
            state.error = action.error.message;
        });
    },
});

export default streamsSlice.reducer;