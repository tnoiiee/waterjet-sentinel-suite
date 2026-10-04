// WJSS Stage 0.2.1A — entry. Connects to the synthetic harness over SSE (same origin).
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { PresentationStore } from './store/presentationStore';
import { StoreContext } from './store/hooks';
import { SseFeed } from './store/feed';
import './global.css';

const store = new PresentationStore();
const feed = new SseFeed(store, { url: '/api/stream' });
feed.start();

createRoot(document.getElementById('root')!).render(
  <StoreContext.Provider value={store}>
    <App />
  </StoreContext.Provider>,
);
