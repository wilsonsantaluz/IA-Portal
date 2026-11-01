import "./index.css";
import { PortalThemeProvider } from "./theme/PortalThemeProvider";
import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { AuthProvider } from "./auth/AuthProvider";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <PortalThemeProvider><AuthProvider>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </AuthProvider></PortalThemeProvider>
  </React.StrictMode>
);
