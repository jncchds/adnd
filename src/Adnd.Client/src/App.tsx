import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import LoginPage from '@/pages/auth/LoginPage';
import RegisterPage from '@/pages/auth/RegisterPage';
import SettingsPage from '@/pages/settings/SettingsPage';
import LlmPresetsPage from '@/pages/llm-presets/LlmPresetsPage';
import SystemRegistryPage from '@/pages/systems/SystemRegistryPage';
import GameManagementPage from '@/pages/games/GameManagementPage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/llm-presets" element={<LlmPresetsPage />} />
        <Route path="/systems" element={<SystemRegistryPage />} />
        <Route path="/games" element={<GameManagementPage />} />
        <Route path="/dashboard" element={<Navigate to="/games" replace />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
