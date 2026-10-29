import React from 'react';import {createRoot} from 'react-dom/client';import App from './App.jsx';import './style.css';
import {WelcomeGate} from './Welcome.jsx';
createRoot(document.getElementById('root')).render(<WelcomeGate><App/></WelcomeGate>);
