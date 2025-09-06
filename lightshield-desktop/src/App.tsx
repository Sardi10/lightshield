import { Routes, Route } from 'react-router-dom'
import Onboarding from './pages/Onboarding'
import Dashboard from './pages/Dashboard'
import ConfigEditor from './pages/ConfigEditor';
import EventStream from "./pages/EventStream";
import AlertLog from "./pages/AlertLog";


export default function App() {
    return (
        <Routes>
            <Route path="/config" element={<ConfigEditor />} />
            <Route path="/" element={<Onboarding />} />
            <Route path="/events" element={<EventStream />} />
            <Route path="/alerts" element={<AlertLog />} />
            <Route path="/dashboard" element={<Dashboard />} />
        </Routes>
    )
}
