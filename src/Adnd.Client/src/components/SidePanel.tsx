import { useNavigate, useParams, useLocation } from 'react-router-dom';
import {
  Dashboard as DashboardIcon,
  AutoAwesome as LLMIcon,
  Settings as SettingsIcon,
  Logout as LogoutIcon,
  Login as LoginIcon,
  SportsEsports as GameIcon,
  Shield as ShieldIcon,
  ArrowBack as BackIcon,
  Chat as ChatIcon,
  Add as AddIcon,
  PlayArrow as PlayArrowIcon,
  People as PeopleIcon,
  Article as SheetIcon,
  Lightbulb as BulbIcon,
  History as HistoryIcon,
  AutoFixHigh as ConsistencyIcon,
  Mic as MicIcon,
  Menu as MenuIcon,
  DarkMode as DarkModeIcon,
  LightMode as LightModeIcon,
} from '@mui/icons-material';
import { useAuth } from '../api/hooks/useAuth';
import { useColorMode } from '../main';
import type { GameListItem, LLMPreset } from '../types';

export type AppView = 'welcome' | 'dashboard' | 'llm-presets' | 'systems' | 'user-settings' | 'game' | 'admin';

interface SidePanelProps {
  open: boolean;
  onToggle: () => void;
  currentView: AppView;
  onNavigate: (view: AppView) => void;
  gameId?: string;
  onNewGame?: () => void;
  onJoinGame?: () => void;
  onAddPreset?: () => void;
  onNewSystem?: () => void;
  games?: GameListItem[];
  presets?: LLMPreset[];
  isMobile?: boolean;
}

export default function SidePanel({
  open, onToggle, currentView, onNavigate, gameId,
  onNewGame, onJoinGame, onAddPreset, onNewSystem,
  games, presets, isMobile = false,
}: SidePanelProps) {
  const navigate = useNavigate();
  const { id: adminId } = useParams<{ id: string }>();
  const location = useLocation();
  const { user, isAuthenticated, logout } = useAuth();
  const { mode, toggle: toggleTheme } = useColorMode();

  const activeGames = games?.filter(g => g.status !== 'Archived' && g.status !== 'Finished') ?? [];
  const resolvedPresets = presets ?? [];

  const handleLogout = async () => {
    await logout();
    onNavigate('welcome');
  };

  const cls = `app-sidebar ${open ? 'expanded' : 'collapsed'}`;

  const Btn = ({
    icon, label, onClick, active = false, danger = false, extraStyle,
  }: {
    icon: React.ReactNode;
    label: string;
    onClick?: () => void;
    active?: boolean;
    danger?: boolean;
    extraStyle?: React.CSSProperties;
  }) => (
    <button
      className={`sidebar-btn${active ? ' active' : ''}${danger ? ' danger' : ''}`}
      onClick={onClick}
      style={extraStyle}
      title={label}
    >
      <span className="s-icon">{icon}</span>
      <span className="s-label">{label}</span>
    </button>
  );

  return (
    <>
      {isMobile && open && (
        <div className="sidebar-overlay" onClick={onToggle} />
      )}
      <aside className={cls}>

        {/* Fixed top: hamburger + title + version + ALPHA */}
        <div className="sidebar-fixed-top">
          <button
            className="sidebar-btn sidebar-toggle-btn"
            onClick={onToggle}
            title={open ? 'Collapse sidebar' : 'Expand sidebar'}
            style={{ fontWeight: 700 }}
          >
            <span className="s-icon"><MenuIcon fontSize="small" /></span>
            {open && (
              <>
                <span className="s-label" style={{ color: 'var(--accent)', letterSpacing: '0.1em' }}>ADnD</span>
                <span className="version-pill">v{__APP_VERSION__}</span>
                <span className="beta-badge">ALPHA</span>
              </>
            )}
          </button>
        </div>

        {/* Scrollable nav */}
        <div className="sidebar-scroll">

          {/* Auth row */}
          {isAuthenticated ? (
            <>
              <div className="sidebar-section-title">
                <span className="s-label">{user?.displayName ?? 'User'}</span>
              </div>
              <Btn icon={<LogoutIcon fontSize="small" />} label="Log out" onClick={handleLogout} danger />
            </>
          ) : (
            <>
              <Btn icon={<LoginIcon fontSize="small" />} label="Log in" onClick={() => navigate('/login')} />
              <Btn icon={<AddIcon fontSize="small" />} label="Register" onClick={() => navigate('/register')} />
            </>
          )}

          <div className="sidebar-divider" />

          {/* Main nav */}
          {isAuthenticated && (
            <>
              <Btn
                icon={<DashboardIcon fontSize="small" />}
                label="Games"
                onClick={() => onNavigate('dashboard')}
                active={currentView === 'dashboard'}
              />
              <Btn
                icon={<LLMIcon fontSize="small" />}
                label="LLM Presets"
                onClick={() => onNavigate('llm-presets')}
                active={currentView === 'llm-presets'}
              />
              <Btn
                icon={<GameIcon fontSize="small" />}
                label="Systems"
                onClick={() => onNavigate('systems')}
                active={currentView === 'systems'}
              />
              <Btn
                icon={<SettingsIcon fontSize="small" />}
                label="User Settings"
                onClick={() => onNavigate('user-settings')}
                active={currentView === 'user-settings'}
              />

              <div className="sidebar-divider" />
            </>
          )}

          {/* Dashboard: actions + game list */}
          {currentView === 'dashboard' && open && isAuthenticated && (
            <>
              {onNewGame && (
                <Btn icon={<AddIcon fontSize="small" />} label="New Game" onClick={onNewGame} />
              )}
              {onJoinGame && (
                <Btn icon={<PlayArrowIcon fontSize="small" />} label="Join by Code" onClick={onJoinGame} />
              )}
              {activeGames.map(game => (
                <Btn
                  key={game.id}
                  icon={<GameIcon fontSize="small" />}
                  label={game.name}
                  onClick={() => navigate(`/game/${game.id}`)}
                />
              ))}
            </>
          )}

          {/* LLM Presets view */}
          {currentView === 'llm-presets' && open && isAuthenticated && (
            <>
              {onAddPreset && (
                <Btn icon={<AddIcon fontSize="small" />} label="Add Preset" onClick={onAddPreset} />
              )}
              {resolvedPresets.length === 0 ? (
                <div className="sidebar-section-title"><span className="s-label">No presets yet</span></div>
              ) : (
                resolvedPresets.map(preset => (
                  <Btn
                    key={preset.id}
                    icon={<LLMIcon fontSize="small" />}
                    label={preset.name}
                    onClick={() => navigate(`/llm-presets/${preset.id}`)}
                    active={location.pathname === `/llm-presets/${preset.id}`}
                  />
                ))
              )}
            </>
          )}

          {/* Systems view */}
          {currentView === 'systems' && open && isAuthenticated && (
            <>
              {onNewSystem && (
                <Btn icon={<AddIcon fontSize="small" />} label="New System" onClick={onNewSystem} />
              )}
            </>
          )}

          {/* Game view */}
          {currentView === 'game' && (
            <>
              <Btn icon={<BackIcon fontSize="small" />} label="Back" onClick={() => onNavigate('dashboard')} />
              {gameId && (
                <Btn
                  icon={<ShieldIcon fontSize="small" />}
                  label="Admin"
                  onClick={() => navigate(`/admin/${gameId}`)}
                />
              )}
              {open && gameId && (
                <>
                  <div className="sidebar-divider" />
                  <Btn
                    icon={<ChatIcon fontSize="small" />}
                    label="Chat"
                    onClick={() => navigate(`/game/${gameId}`)}
                    active={location.pathname === `/game/${gameId}`}
                  />
                  <Btn
                    icon={<SettingsIcon fontSize="small" />}
                    label="Settings"
                    onClick={() => navigate(`/game/${gameId}/settings`)}
                    active={location.pathname === `/game/${gameId}/settings`}
                  />
                </>
              )}
            </>
          )}

          {/* Admin view */}
          {currentView === 'admin' && (
            <>
              <Btn icon={<BackIcon fontSize="small" />} label="Back to Game" onClick={() => onNavigate('game')} />
              {open && adminId && (
                <>
                  <div className="sidebar-divider" />
                  <Btn
                    icon={<DashboardIcon fontSize="small" />}
                    label="Dashboard"
                    onClick={() => navigate(`/admin/${adminId}`)}
                    active={location.pathname === `/admin/${adminId}`}
                  />
                  <Btn
                    icon={<BulbIcon fontSize="small" />}
                    label="Plot Board"
                    onClick={() => navigate(`/admin/${adminId}/plot-board`)}
                    active={location.pathname === `/admin/${adminId}/plot-board`}
                  />
                  <Btn
                    icon={<PeopleIcon fontSize="small" />}
                    label="NPCs"
                    onClick={() => navigate(`/admin/${adminId}/npcs`)}
                    active={location.pathname === `/admin/${adminId}/npcs`}
                  />
                  <Btn
                    icon={<SheetIcon fontSize="small" />}
                    label="Characters"
                    onClick={() => navigate(`/admin/${adminId}/characters`)}
                    active={location.pathname === `/admin/${adminId}/characters`}
                  />
                  <div className="sidebar-divider" />
                  <div className="sidebar-section-title">
                    <span className="s-label">Tools</span>
                  </div>
                  <Btn
                    icon={<ConsistencyIcon fontSize="small" />}
                    label="Consistency Check"
                    onClick={() => navigate(`/admin/${adminId}/consistency`)}
                    active={location.pathname === `/admin/${adminId}/consistency`}
                  />
                  <Btn
                    icon={<HistoryIcon fontSize="small" />}
                    label="LLM Logs"
                    onClick={() => navigate(`/admin/${adminId}/llm-logs`)}
                    active={location.pathname === `/admin/${adminId}/llm-logs`}
                  />
                  <Btn
                    icon={<MicIcon fontSize="small" />}
                    label="Agent Calls"
                    onClick={() => navigate(`/admin/${adminId}/agent-calls`)}
                    active={location.pathname === `/admin/${adminId}/agent-calls`}
                  />
                </>
              )}
            </>
          )}
        </div>

        {/* Fixed bottom: theme toggle only */}
        <div className="sidebar-fixed-bottom">
          <Btn
            icon={mode === 'dark' ? <LightModeIcon fontSize="small" /> : <DarkModeIcon fontSize="small" />}
            label={mode === 'dark' ? 'Light mode' : 'Dark mode'}
            onClick={toggleTheme}
          />
        </div>
      </aside>
    </>
  );
}
