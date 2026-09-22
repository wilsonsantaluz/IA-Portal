import { Outlet } from "react-router-dom";

import React, { useState } from "react";
import Sidebar from "./Sidebar";
import HeaderBar from "./HeaderBar";
import RequireAuth from "../auth/RequireAuth";


export default function PrivateLayout() {
    const [sidebarOpen, setSidebarOpen] = useState(false);
  return (
    <RequireAuth>
      <div >
      <Sidebar open={sidebarOpen} setOpen={setSidebarOpen} />



        <div style={{ flex: 1 }}>
        <HeaderBar open={sidebarOpen} setOpen={setSidebarOpen} />
          <Outlet />
        </div>
      </div>
    </RequireAuth>
  );
}
