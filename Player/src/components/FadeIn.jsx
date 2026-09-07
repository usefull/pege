import { useEffect, useState, useTransition } from 'react';
import { useLocation } from 'react-router-dom';

import '../styles/fade-in.scss'

function FadeIn({ children }) {
    const location = useLocation();
    const [, startTransition] = useTransition();
    const [isVisible, setIsVisible] = useState(false);
  
    useEffect(() => {
        startTransition(() => {
            const timer = setTimeout(() => setIsVisible(true), 30);
            return () => clearTimeout(timer);
        });
    }, [location.key]);
  
  return (
    <div className={`fade-in ${isVisible ? 'done' : ''}`}>
      {children}
    </div>
  );
}

export default FadeIn;