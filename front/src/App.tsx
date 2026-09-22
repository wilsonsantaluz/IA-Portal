import { Routes, Route, Navigate } from "react-router-dom";

import LoginPage from "./pages/LoginPage";
import ChatPage from "./pages/ChatPage";
import AdminDashboardPage from "./pages/AdminDashboardPage";
import KnowledgeBasePage from "./pages/KnowledgeBasePage";

import RequireAuth from "./auth/RequireAuth";
import PublicRoute from "./auth/PublicRoute";

import DashboardLayout from "./layout/DashboardLayout";
import React from "react";

export default function App() {
  return (
    <Routes>
      {/* LOGIN SEM LAYOUT */}
      <Route
        path="/login"
        element={
          <PublicRoute>
            <LoginPage />
          </PublicRoute>
        }
      />

      {/* APP PROTEGIDA (COM LAYOUT) */}
      <Route
        path="/"
        element={
          <RequireAuth>
            <DashboardLayout />
          </RequireAuth>
        }
      >
        <Route path="chat" element={<ChatPage />} />
        <Route path="dashboard" element={<AdminDashboardPage />} />
        <Route path="kb" element={<KnowledgeBasePage />} />
      </Route>

   
    </Routes>
  );
}
