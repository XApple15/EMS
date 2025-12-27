import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { HashRouter } from 'react-router-dom';

import Navbar from './components/Navbar';
import Login from './components/Login';
import Register from './components/Register';
import ClientDashboard from './components/ClientDashboard';
import AdminDashboard from './components/AdminDashboard';
import EnergyConsumptionHistory from './components/EnergyConsumptionHistory';
import './App.css';

function App() {

    return (
   
      <Router>
          <div className="App">
              <Navbar />
              <div className="content">
                  <Routes>
                      <Route path="/" element={<Navigate to="/login" replace />} />
                      <Route path="/login" element={<Login />} />
                      <Route path="/register" element={<Register />} />
                      <Route path="/client/dashboard" element={<ClientDashboard />} />
                      <Route path="/admin/dashboard" element={<AdminDashboard />} />
                      <Route path="/client/energy-history" element={<EnergyConsumptionHistory />} />
                  </Routes>
              </div>
          </div>
            </Router>
  

  )
}

export default App
