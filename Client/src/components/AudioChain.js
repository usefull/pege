export default class AudioChain {
  constructor(context, isEqEnabled, eqGrains, centralFreqs) {
    this.context = context;
    this.isEqEnabled = isEqEnabled;

    // Финальный мастер-выход
    this.masterGain = context.createGain();
    this.masterGain.gain.setValueAtTime(0.001, context.currentTime);

    // Защита от клиппинга
    this.limiter = context.createDynamicsCompressor();
    this.limiter.threshold.setValueAtTime(-0.5, context.currentTime);
    this.limiter.knee.setValueAtTime(5, context.currentTime);
    this.limiter.ratio.setValueAtTime(15, context.currentTime);
    this.limiter.attack.setValueAtTime(0.001, context.currentTime);
    this.limiter.release.setValueAtTime(0.1, context.currentTime);
    
    // Подключаем лимитер к мастер-выходу
    this.limiter.connect(this.masterGain);

    // Узел-заглушка для "сухого" пути (Bypass) идет в лимитер
    this.dryBus = context.createGain();
    this.dryBus.gain.value = 1;
    this.dryBus.connect(this.limiter);

    // Массив эквалайзера (последовательное соединение)
    this.eqFilters = [];
    for (let i = 0; i < centralFreqs.length; i++) {
      const filter = context.createBiquadFilter();
      filter.type = 'peaking';
      filter.frequency.value = centralFreqs[i];
      filter.Q.value = 1.0;
      filter.gain.value = eqGrains[i];

      if (i > 0) {
        this.eqFilters[i - 1].connect(filter);
      }
      this.eqFilters.push(filter);
    }

    // Последний фильтр эквалайзера подключаем к лимитеру
    this.eqFilters[centralFreqs.length - 1].connect(this.limiter);

    // Входной разветвитель
    this.preFXSplitter = context.createGain();
    if (this.isEqEnabled) {
      this.preFXSplitter.connect(this.eqFilters[0]);
    } else {
      this.preFXSplitter.connect(this.dryBus);
    }

    this.input = this.preFXSplitter;
  }

  /** Подключение источника звука */
  connectSource(source) {
    source.connect(this.input);
  }

  /** Управление включением/выключением всей цепи обработки */
  setEqualizerOn(enabled) {
    if (this.isEqEnabled === enabled) return;
    this.isEqEnabled = enabled;

    this.preFXSplitter.disconnect();

    if (enabled) {
      this.preFXSplitter.connect(this.eqFilters[0]);
    } else {
      this.preFXSplitter.connect(this.dryBus);
    }
  }

  /** Установка значений эквалайзера */
  setEqGains(gains) {
    gains.forEach((gain, index) => {
      if (this.eqFilters[index]) {
        this.eqFilters[index].gain.setTargetAtTime(gain, this.context.currentTime, 0.01);
      }
    });
  }

  /** Освобождение ресурсов */
  destroy() {
    this.masterGain.disconnect();
    this.limiter.disconnect(); // Отключаем лимитер
    this.dryBus.disconnect(); // Отключаем dryBus явным образом
    this.eqFilters.forEach(f => f.disconnect());
  }
}