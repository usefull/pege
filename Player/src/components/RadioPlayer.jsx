import { useEffect, useRef } from 'react';
import { MPEGDecoder } from 'mpg123-decoder';
import { decoder as createAACDecoder } from '@audio/decode-aac';
import AudioChain from './AudioChain';

const MIN_BUFFER_DURATION = 2.0;
const LOOK_AHEAD_TIME = 0.6;
const CONSUMER_TICK_MS = 50;
const INITIAL_RECONNECT_DELAY = 1000;
const MAX_RECONNECT_DELAY = 16000;
const FADE_DURATION = 0.4;

const RadioPlayer = ({ 
  streamUrl, 
  setIsPlaying, 
  onToggleReady, 
  onBuffering, 
  equalizerOn, 
  centralFreqs, 
  eqGrains,
  onStreamInfoUpdate
}) => {
  const audioContextRef = useRef(null);
  const decoderRef = useRef(null);
  const decoderTypeRef = useRef(null); // 'mp3' или 'aac'
  const abortControllerRef = useRef(null);
  
  const isPlayingRef = useRef(false);
  const isStoppingRef = useRef(false);
  const nextStartTimeRef = useRef(0);
  const streamUrlRef = useRef(streamUrl);

  const equalizerOnRef = useRef(equalizerOn);
  const eqGrainsRef = useRef(eqGrains);

  const audioQueueRef = useRef([]);
  const isBufferingRef = useRef(true);
  const reconnectTimeoutRef = useRef(null);
  const consumerTimerRef = useRef(null);

  const wakeLockRef = useRef(null);
  const visibilityHandlerRef = useRef(null);

  const audioChainRef = useRef(null);
  const isEqEnabledRef = useRef(true);

  const metadataRef = useRef(null);
  const streamInfoRef = useRef({
    Name: null,
    Country: null,
    Track: null,
    Artist: null,
    FromFlac: false,
    Next: null
  });

  // === Эффекты для обновления параметров ===
  useEffect(() => {
    equalizerOnRef.current = equalizerOn;
    if (audioChainRef.current)
      audioChainRef.current.setEqualizerOn(equalizerOn);
  }, [equalizerOn]);

  useEffect(() => {
    eqGrainsRef.current = eqGrains;
    if (audioChainRef.current)
      audioChainRef.current.setEqGains(eqGrains);
  }, [eqGrains]);

    useEffect(() => {
        if (streamUrlRef.current !== streamUrl) {
            streamInfoRef.current = { ... streamInfoRef.current,
                Country: null,
                Artist: null,
                Track: null,
                FromFlac: false,
                Next: null
            };
            if (onStreamInfoUpdate)
                onStreamInfoUpdate(streamInfoRef.current);
        }
        streamUrlRef.current = streamUrl;
    }, [streamUrl]);

    const togglePlay = async () => {
        if (isPlayingRef.current) {
            await stopPlaybackWithFade();            
        } else {
            await startPlayback();
        }
    };

  // === Wake Lock ===
  const requestWakeLock = async () => {
    if (!('wakeLock' in navigator)) return;
    try {
      if (wakeLockRef.current && !wakeLockRef.current.released) return;
      wakeLockRef.current = await navigator.wakeLock.request('screen');
      wakeLockRef.current.addEventListener('release', () => {
        if (isPlayingRef.current) {
          setTimeout(() => { if (isPlayingRef.current) requestWakeLock(); }, 100);
        }
      });
    } catch (err) {
      console.warn(`Wake Lock failed: ${err.name}, ${err.message}`);
    }
  };

  const restoreWakeLockIfNeeded = async () => {
    if (isPlayingRef.current) {
      const hasValidLock = wakeLockRef.current && !wakeLockRef.current.released;
      if (!hasValidLock) await requestWakeLock();
    }
  };

  const releaseWakeLock = async () => {
    if (wakeLockRef.current) {
      try { await wakeLockRef.current.release(); } catch (e) { console.error(e); }
      wakeLockRef.current = null;
    }
  };

  // === Fade-out ===
  const stopPlaybackWithFade = () => {
    return new Promise((resolve) => {
      if (!isPlayingRef.current || isStoppingRef.current) {
        resolve();
        return;
      }

      isStoppingRef.current = true;

      if (audioContextRef.current && audioChainRef.current) {
        const ctx = audioContextRef.current;
        const gain = audioChainRef.current.masterGain.gain;

        gain.cancelScheduledValues(ctx.currentTime);
        gain.setValueAtTime(gain.value, ctx.currentTime);
        gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + FADE_DURATION);
      }

      setTimeout(() => {
        forceStopAll();
        isStoppingRef.current = false;
        resolve();
      }, FADE_DURATION * 1000);
    });
  };

  // === Полная остановка ===
  const forceStopAll = () => {
    isPlayingRef.current = false;
    setIsPlaying(false);
    isBufferingRef.current = true;

    releaseWakeLock();

    if (visibilityHandlerRef.current) {
      document.removeEventListener('visibilitychange', visibilityHandlerRef.current);
      if (visibilityHandlerRef.current._resumeHandler) {
        document.removeEventListener('resume', visibilityHandlerRef.current._resumeHandler);
      }
      visibilityHandlerRef.current = null;
    }

    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }

    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }

    if (consumerTimerRef.current) {
      clearTimeout(consumerTimerRef.current);
      consumerTimerRef.current = null;
    }

    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      audioContextRef.current.close();
      audioContextRef.current = null;
    }

    if (audioChainRef.current) {
      try {
        audioChainRef.current.destroy();
      } catch (e) {
        console.warn('Error during AudioChain cleanup:', e);
      }
      audioChainRef.current = null;
      isEqEnabledRef.current = true;
    }

    if (decoderRef.current) {
      if (decoderRef.current.mp3 && typeof decoderRef.current.mp3.free === 'function') {
        decoderRef.current.mp3.free();
      }
      if (decoderRef.current.aac && typeof decoderRef.current.aac.free === 'function') {
        decoderRef.current.aac.free();
      } else if (decoderRef.current.aac && typeof decoderRef.current.aac.destroy === 'function') {
        decoderRef.current.aac.destroy();
      }
      decoderRef.current = null;
    }
    decoderTypeRef.current = null;

    nextStartTimeRef.current = 0;
    audioQueueRef.current = [];
    
    if (onBuffering) onBuffering(false);
  };

    const connectAndReadStream = async (currentDelay) => {
        if (!isPlayingRef.current) return;
        const currentUrl = streamUrlRef.current;
        abortControllerRef.current = new AbortController();

        try {
            const response = await fetch(currentUrl, {
                signal: abortControllerRef.current.signal,
                // Запрашиваем поддерживаемые форматы и метаданные
                headers: {
                    'Accept': 'audio/mpeg, audio/aac, audio/x-aac, audio/aacp',
                    'Icy-MetaData': '1'
                }
            });
        
            if (!response.body) throw new Error('Empty stream body');
            
            // Динамическое определение типа по Content-Type заголовку сервера
            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('aac') || contentType.includes('audio/aacp')) {
                decoderTypeRef.current = 'aac';
            } else if (contentType.includes('mpeg') || contentType.includes('mp3')) {
                decoderTypeRef.current = 'mp3';
            } else {
                // Резервный вариант, если сервер не прислал заголовок — смотрим по URL
                const lowerUrl = currentUrl.toLowerCase();
                decoderTypeRef.current = (lowerUrl.includes('aac') || lowerUrl.includes('aacp')) ? 'aac' : 'mp3';
            }

            let metaInterval = parseInt(response.headers.get('icy-metaint') || '', 10);
            metaInterval = isNaN(metaInterval) ? 0 : metaInterval;

            const icyNameHeader = response.headers.get('icy-name') || '';
            const parts = decodeURIComponent(icyNameHeader).split('|||');
            streamInfoRef.current = { ... streamInfoRef.current };
            if (parts.length > 0) {
                streamInfoRef.current.Name = parts[0].trim();
            }
            if (parts.length > 1) {
                streamInfoRef.current.Country = parts[1].trim();
            }
            if (onStreamInfoUpdate)
                onStreamInfoUpdate(streamInfoRef.current);

            const reader = response.body.getReader();

            let metadataLeft = 0;
            let metadataLength = 0;
            let bytesAfterMetadata = 0;

            while (isPlayingRef.current) {
                const { done, value } = await reader.read();
                if (done) break;
                
                const bytesRead = value.length;

                // Если тип сбросился при ошибке, пытаемся определить по сигнатуре первых байт чанка
                if (!decoderTypeRef.current && value && value.length > 1) {
                    // 0xFFF или 0xFFF9 — сигнатура ADTS фрейма AAC. 0xFFFB — MP3 фрейм.
                    if (value[0] === 0xFF && (value[1] & 0xF0) === 0xF0) {
                        decoderTypeRef.current = (value[1] & 0x06) === 0x00 ? 'aac' : 'mp3';
                    } else {
                        decoderTypeRef.current = 'mp3'; // По умолчанию
                    }
                }
                
                if (metaInterval > 0) {
                    let bufferPos = 0;
                    while (bufferPos < bytesRead) {
                        if (metadataLeft > 0) {
                            // нужно читать метаданные
                            if (metadataLeft <= bytesRead - bufferPos) {
                                // метаданные заканчиваются в текущем батче
                                metadataRef.current.set(
                                    value.subarray(bufferPos, bufferPos + metadataLeft),
                                    metadataLength - metadataLeft
                                );
                                bufferPos += metadataLeft;
                                metadataLeft = 0;

                                const strMetadata = new TextDecoder().decode(metadataRef.current);
                                const metadataParts = strMetadata.split(';');
                                const streamTitle = metadataParts.find(s => s.startsWith("StreamTitle='"));
                                streamInfoRef.current = { ... streamInfoRef.current };
                                if (!streamTitle) {
                                    streamInfoRef.current.Track = null;
                                    streamInfoRef.current.Artist = null;
                                } else {
                                    const start = streamTitle.indexOf("'") + 1;
                                    const end = streamTitle.lastIndexOf("'");

                                    if (start < 1 || end < 0) {
                                        streamInfoRef.current.Track = '';
                                        streamInfoRef.current.Artist = '';
                                    } else {
                                        const parts = streamTitle
                                            .substring(start, end)
                                            .split(' - ')
                                            .map(s => s.trim());
                                        
                                        if (parts.length > 0) {
                                            streamInfoRef.current.Artist = parts[0];
                                        }
                                        
                                        if (parts.length > 1) {
                                            streamInfoRef.current.Track = parts[1];
                                        }
                                    }
                                }
                                ///////////////////
                                const streamNext = metadataParts.find(s => s.startsWith("NextTrack='"));
                                streamInfoRef.current = { ... streamInfoRef.current };
                                if (!streamNext) {
                                    streamInfoRef.current.Next = null;
                                } else {
                                    const start = streamNext.indexOf("'") + 1;
                                    const end = streamNext.lastIndexOf("'");

                                    if (start < 1 || end < 0) {
                                        streamInfoRef.current.Next = '';
                                    } else {
                                        const parts = streamNext
                                            .substring(start, end)
                                            .split(' - ')
                                            .map(s => s.trim());
                                        
                                        let a = null;
                                        let t = null;
                                        if (parts.length > 0) {
                                            a = parts[0];
                                        }
                                        
                                        if (parts.length > 1) {
                                            t = parts[1];
                                        }

                                        if (t && t.length > 0) {
                                            streamInfoRef.current.Next = `"${t}"${(a && a.length > 0) ? ` by ${a}` : ''}`;
                                        } else {
                                            streamInfoRef.current.Next = null;
                                        }
                                    }
                                }
                                ///////////////////
                                const fromFlac = metadataParts.find(s => s.startsWith("FromFlac='"));
                                if (!fromFlac) {
                                    streamInfoRef.current.FromFlac = false;
                                } else {
                                    const start = fromFlac.indexOf("'") + 1;
                                    const end = fromFlac.lastIndexOf("'");

                                    if (start < 1 || end < 0) {
                                        streamInfoRef.current.FromFlac = false;
                                    } else if (fromFlac.substring(start, end) === "1") {
                                        streamInfoRef.current.FromFlac = true;
                                    } else {
                                        streamInfoRef.current.FromFlac = false;
                                    }
                                }
                                if (onStreamInfoUpdate) {
                                    onStreamInfoUpdate(streamInfoRef.current);
                                }
                                
                                console.log('Метаданные: ' + strMetadata + ' ||| длина: ' + metadataLength);
                                metadataRef.current = null;
                            } else {
                                // метаданные переходят на следующий батч
                                metadataRef.current.set(
                                    value.subarray(bufferPos, bufferPos + (bytesRead - bufferPos)),
                                    metadataLength - metadataLeft
                                );                                
                                metadataLeft -= bytesRead - bufferPos;
                                bufferPos += bytesRead - bufferPos;
                            }
                        } else {
                            // мотаем дальше
                            if (metaInterval - bytesAfterMetadata < bytesRead - bufferPos) {
                                // метаданные есть в оставшемся батче
                                if (metaInterval - bytesAfterMetadata > 0) {
                                    // есть аудиоданные в начале текущего батча
                                    ///////////////////////////////////////////
                                    /// ОТПРАВКА АУДИТОДАННЫХ
                                    const buff = new Uint8Array(new ArrayBuffer(metaInterval - bytesAfterMetadata));
                                    buff.set(
                                        value.subarray(bufferPos, bufferPos + (metaInterval - bytesAfterMetadata)),
                                        0
                                    );
                                    if (!await decodeAndEnqueue(buff)) continue;
                                    ///////////////////////////////////////////
                                }

                                bufferPos += metaInterval - bytesAfterMetadata;
                                metadataLength = value[bufferPos] * 16;
                                bufferPos += 1;
                                if (metadataLength == 0) {
                                    bytesAfterMetadata = 0;
                                }
                                else
                                {
                                    metadataRef.current = new Uint8Array(new ArrayBuffer(metadataLength));
                                    metadataLeft = metadataLength;
                                    bytesAfterMetadata = 0;
                                }
                            } else {
                                // метаданных в оставшемся батче нет
                                ///////////////////////////////////////////
                                /// ОТПРАВКА АУДИТОДАННЫХ
                                const buff = new Uint8Array(new ArrayBuffer(bytesRead - bufferPos));
                                buff.set(
                                    value.subarray(bufferPos, bufferPos + (bytesRead - bufferPos)),
                                    0
                                );
                                if (!await decodeAndEnqueue(buff)) continue;
                                ///////////////////////////////////////////
                                bytesAfterMetadata += bytesRead - bufferPos;
                                bufferPos += bytesRead - bufferPos;
                            }
                        }
                    }
                } else {
                    if (!await decodeAndEnqueue(value)) continue;
                }
            }
        } catch (error) {
            if (error.name === 'AbortError') return;
            console.warn(`Stream network error. Reconnecting in ${currentDelay}ms...`, error);
            if (abortControllerRef.current) abortControllerRef.current.abort();

            const nextDelay = Math.min(currentDelay * 2, MAX_RECONNECT_DELAY);
            reconnectTimeoutRef.current = setTimeout(() => connectAndReadStream(nextDelay), currentDelay);
        }
        finally {
            metadataRef.current = null;
        }
    };

    const decodeAndEnqueue = async (value) => {
        try {
            let decoded = null;
            
            if (decoderTypeRef.current === 'mp3' && decoderRef.current?.mp3) {
                decoded = decoderRef.current.mp3.decode(value);
                if (!decoded || decoded.samplesDecoded === 0 || !decoded.channelData) {
                    // console.error(
                    //     `❌ [Decoder Error] Ошибка декодирования чанка! ` +
                    //     `Размер битого куска: ${value.length} байт. ` +
                    //     `Первые байты (HEX): ${Array.from(value.subarray(0, 10)).map(b => b.toString(16).padStart(2, '0')).join(' ')}`
                    // );
                    return true; // Пропускаем, чтобы плеер не завис
                }
            } else if (decoderTypeRef.current === 'aac' && decoderRef.current?.aac) {
                // Декодирование AAC в этой библиотеке асинхронное
                decoded = await decoderRef.current.aac.decode(value); 
            } else {
                return false;
            }

            if (decoded && decoded.channelData && decoded.channelData.length > 0) {
                const sampleRate = decoded.sampleRate;
                let leftData, rightData;
                
                if (Array.isArray(decoded.channelData)) {
                    leftData = decoded.channelData[0];
                    rightData = decoded.channelData.length > 1 ? decoded.channelData[1] : decoded.channelData[0];
                } else {
                    leftData = decoded.channelData;
                    rightData = decoded.channelData;
                }

                const samplesCount = leftData.length;
                if (samplesCount === 0) return false;
                
                const duration = samplesCount / sampleRate;
                audioQueueRef.current.push({ leftData, rightData, samplesCount, sampleRate, duration });
            }
            return true;
        } catch (decodeError) {
            console.warn('Decode error:', decodeError);
            decoderTypeRef.current = null; 
            return false;
        }
    };

  // === Consumer ===
  const runConsumer = () => {
    if (!isPlayingRef.current || !audioContextRef.current) return;

    const context = audioContextRef.current;
    const totalQueueDuration = audioQueueRef.current.reduce((sum, chunk) => sum + chunk.duration, 0);

    if (isBufferingRef.current) {
      if (totalQueueDuration >= MIN_BUFFER_DURATION) {
        isBufferingRef.current = false;
        if (onBuffering) onBuffering(false);
        nextStartTimeRef.current = context.currentTime + 0.05;

        if (audioChainRef.current) {
          const gain = audioChainRef.current.masterGain.gain;
          gain.cancelScheduledValues(context.currentTime);
          gain.setValueAtTime(0.001, context.currentTime);
          gain.exponentialRampToValueAtTime(1.0, context.currentTime + FADE_DURATION);
        }
      } else {
        consumerTimerRef.current = setTimeout(runConsumer, CONSUMER_TICK_MS);
        return;
      }
    }

    if (audioQueueRef.current.length === 0 && nextStartTimeRef.current < context.currentTime) {
      isBufferingRef.current = true;
      if (onBuffering) onBuffering(true);
      
      if (audioChainRef.current) {
        audioChainRef.current.masterGain.gain.setValueAtTime(0.001, context.currentTime);
      }

      consumerTimerRef.current = setTimeout(runConsumer, CONSUMER_TICK_MS);
      return;
    }

    while (
      audioQueueRef.current.length > 0 && 
      (nextStartTimeRef.current - context.currentTime) < LOOK_AHEAD_TIME
    ) {
      const chunk = audioQueueRef.current.shift();

      const audioBuffer = context.createBuffer(2, chunk.samplesCount, chunk.sampleRate);
      audioBuffer.getChannelData(0).set(chunk.leftData);
      audioBuffer.getChannelData(1).set(chunk.rightData);

      const bufferSource = context.createBufferSource();
      bufferSource.buffer = audioBuffer;
      
      bufferSource.connect(audioChainRef.current.input);

      bufferSource.onended = () => {
        bufferSource.disconnect();
      };

      const scheduledTime = Math.max(nextStartTimeRef.current, context.currentTime);
      bufferSource.start(scheduledTime);
      nextStartTimeRef.current = scheduledTime + audioBuffer.duration;
    }

    consumerTimerRef.current = setTimeout(runConsumer, CONSUMER_TICK_MS);
  };

    const startPlayback = async () => {
    const currentUrl = streamUrlRef.current;
    if (!currentUrl) return;

    if (onBuffering) onBuffering(true);
    isBufferingRef.current = true;

    await requestWakeLock();

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible' && isPlayingRef.current) restoreWakeLockIfNeeded();
    };

    if (visibilityHandlerRef.current) {
      document.removeEventListener('visibilitychange', visibilityHandlerRef.current);
      if (visibilityHandlerRef.current._resumeHandler) {
        document.removeEventListener('resume', visibilityHandlerRef.current._resumeHandler);
      }
    }

    const handleResume = () => { if (isPlayingRef.current) restoreWakeLockIfNeeded(); };
    document.addEventListener('resume', handleResume);
    handleVisibilityChange._resumeHandler = handleResume;
    visibilityHandlerRef.current = handleVisibilityChange;
    document.addEventListener('visibilitychange', handleVisibilityChange);

    const AudioContextClass = window.AudioContext || window.webkitAudioContext;
    const context = new AudioContextClass();
    audioContextRef.current = context;

    // ПАРАЛЛЕЛЬНАЯ ИНИЦИАЛИЗАЦИЯ
    const mp3Decoder = new MPEGDecoder();
    
    // Запускаем подготовку MP3 и создание AAC декодера параллельно
    const [, aacDecoderInstance] = await Promise.all([
      mp3Decoder.ready,
      createAACDecoder() // Используем вашу функцию из импорта
    ]);
    
    // Сохраняем ссылки на оба готовых инстанса в реф
    decoderRef.current = {
      mp3: mp3Decoder,
      aac: aacDecoderInstance
    };
    
    decoderTypeRef.current = null; 

    audioChainRef.current = new AudioChain(
      context, 
      equalizerOnRef.current, 
      eqGrainsRef.current, 
      centralFreqs
    );
    audioChainRef.current.masterGain.connect(context.destination);

    window.__eqDebug = audioChainRef.current;

    isPlayingRef.current = true;
    setIsPlaying(true);

    runConsumer();
    connectAndReadStream(INITIAL_RECONNECT_DELAY);
  };

  // === Эффекты ===
  useEffect(() => {
    if (onToggleReady) onToggleReady(togglePlay);
    
    return () => {
      forceStopAll();
    };
  }, []);

  // Плавное переключение радиостанций
  useEffect(() => {
    let active = true;
    
    if (isPlayingRef.current) {
      stopPlaybackWithFade().then(() => {
        if (!active) return;
        
        const timer = setTimeout(() => {
          startPlayback();
        }, 50);
        
        return () => clearTimeout(timer);
      });
    }
    
    return () => {
      active = false;
    };
  }, [streamUrl]);

  return null;
};

export default RadioPlayer;