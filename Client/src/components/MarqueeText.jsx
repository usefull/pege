import React, { useState, useEffect, useRef } from 'react';
import '../styles/marquee-text.scss';

const MarqueeText = ({ children }) => {
  const containerRef = useRef(null);
  const textRef = useRef(null);
  const [isOverflowing, setIsOverflowing] = useState(false);

  useEffect(() => {
    const container = containerRef.current;
    const text = textRef.current;

    if (!container || !text) return;

    // Создаем Observer для отслеживания изменения ширины
    const resizeObserver = new ResizeObserver(() => {
      const containerWidth = container.clientWidth;
      const textWidth = text.offsetWidth;

      if (textWidth > containerWidth) {
        setIsOverflowing(true);
        // Передаем текущую ширину контейнера в CSS-переменную
        container.style.setProperty('--container-width', `${containerWidth}px`);
      } else {
        setIsOverflowing(false);
      }
    });

    // Включаем слежку за контейнером
    resizeObserver.observe(container);

    // Чистим за собой при размонтировании компонента
    return () => resizeObserver.disconnect();
  }, [children]); // Перезапускаем, если изменился сам текст

  return (
    <div 
      ref={containerRef} 
      className={`marquee-container ${isOverflowing ? 'is-overflowing' : ''}`}
    >
      <span ref={textRef}>{children}</span>
    </div>
  );
};

export default MarqueeText;
