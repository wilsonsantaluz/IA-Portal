import React, { useEffect, useState } from "react";
import {
    Box,
    Button,
    Container,
    Grid,
    Modal,
    Paper,
    TextField,
    Typography,
    Select,
    MenuItem,
    Stack,
    Backdrop,
    CircularProgress,
    Snackbar,
    Alert,
    IconButton,
} from "@mui/material";

import { DataGrid, GridToolbar } from "@mui/x-data-grid";

import EditIcon from "@mui/icons-material/Edit";
import ToggleOffIcon from "@mui/icons-material/ToggleOff";
import ToggleOnIcon from "@mui/icons-material/ToggleOn";
import AddIcon from "@mui/icons-material/Add";

import ReactMarkdown from "react-markdown";
import api from "../services/api";
interface KbItem {
    id: number;
    section: string;
    subsection: string;
    title: string;
    keyName: string;
    isActive: boolean;
    content?: string;
}

export default function KnowledgeBasePage() {
    const [items, setItems] = useState<KbItem[]>([]);
    const [sections, setSections] = useState<string[]>([]);
    const [search, setSearch] = useState("");

    const [open, setOpen] = useState(false);
    const [editingId, setEditingId] = useState<number | null>(null);

    const [section, setSection] = useState("");
    const [newSection, setNewSection] = useState("");
    const [subsection, setSubsection] = useState("");
    const [title, setTitle] = useState("");
    const [keyName, setKeyName] = useState("");
    const [content, setContent] = useState("");

    const [loading, setLoading] = useState(false);
    const [snack, setSnack] = useState<{
        open: boolean;
        message: string;
        severity: "success" | "error";
    }>({
        open: false,
        message: "",
        severity: "success",
    });

    // ============================================================
    // LOAD DATA
    // ============================================================
    const loadAll = async () => {
        try {
            setLoading(true);
            const res = await api.get("/api/kb/all");
            setItems(res.data || []);
        } catch (err) {
            setSnack({
                open: true,
                message: "Erro ao carregar registros",
                severity: "error",
            });
        } finally {
            setLoading(false);
        }
    };

    const loadSections = async () => {
        try {
            const r = await api.get("/api/kb/sections");
            setSections(r.data || []);
        } catch {
            setSections([]);
        }
    };

    useEffect(() => {
        loadAll();
        loadSections();
    }, []);

    // ============================================================
    // MODAL FORM
    // ============================================================
    const resetForm = () => {
        setEditingId(null);
        setSection("");
        setNewSection("");
        setSubsection("");
        setTitle("");
        setKeyName("");
        setContent("");
    };

    const openModal = async (id?: number) => {
        resetForm();
        await loadSections();

        if (id) {
            const res = await api.get(`/api/kb/${id}`);
            const item = res.data;

            setEditingId(item.id);
            setTitle(item.title || "");
            setKeyName(item.keyName || "");
            setContent(item.content || "");
            setSubsection(item.subsection || "");
            setNewSection(item.section || "");
        }

        setOpen(true);
    };

    const closeModal = () => {
        setOpen(false);
    };

    const save = async () => {
        try {
            setLoading(true);

            const finalSection = newSection || section;

            const body = {
                id: editingId,
                section: finalSection,
                subsection,
                title,
                keyName,
                content,
            };

            await api.post("/api/kb/upsert", body);

            setSnack({
                open: true,
                message: "Registro salvo com sucesso!",
                severity: "success",
            });

            closeModal();
            await loadAll();
        } catch (err: any) {
            const msg =
                err?.response?.data?.error ||
                err?.response?.data?.detail ||
                err.message ||
                "Erro inesperado";

            setSnack({
                open: true,
                message: msg,
                severity: "error",
            });
        } finally {
            setLoading(false);
        }
    };

    const toggle = async (id: number) => {
        try {
            setLoading(true);
            await api.post(`/api/kb/toggle/${id}`);

            setSnack({
                open: true,
                message: "Status alterado com sucesso!",
                severity: "success",
            });

            await loadAll();
        } catch (err: any) {
            const msg =
                err?.response?.data?.error ||
                err?.response?.data?.detail ||
                err.message ||
                "Erro ao alterar status";

            setSnack({
                open: true,
                message: msg,
                severity: "error",
            });
        } finally {
            setLoading(false);
        }
    };



    // ============================================================
    // DATAGRID COLUMNS
    // ============================================================
    const columns = [
        { field: "id", headerName: "ID", width: 70 },
        { field: "section", headerName: "Seção", width: 150 },
        { field: "subsection", headerName: "Subseção", width: 180 },
        { field: "title", headerName: "Título", flex: 1, minWidth: 200 },
        { field: "keyName", headerName: "Key", width: 140 },

        {
            field: "isActive",
            headerName: "Status",
            width: 100,
            renderCell: (params: any) =>
                params.row.isActive ? (
                    <span style={{ color: "green" }}>● Ativo</span>
                ) : (
                    <span style={{ color: "red" }}>● Inativo</span>
                ),
        },

        {
            field: "actions",
            headerName: " ",
            width: 110,
            sortable: false,
            filterable: false,
            align: "right",
            headerAlign: "right",

            renderCell: (params: any) => (
                <Stack direction="row" spacing={1}>
                    <IconButton
                        size="small"
                        color="primary"
                        onClick={() => openModal(params.row.id)}
                    >
                        <EditIcon fontSize="small" />
                    </IconButton>

                    <IconButton
                        size="small"
                        color={params.row.isActive ? "error" : "success"}
                        onClick={() => toggle(params.row.id)}
                    >
                        {params.row.isActive ? (
                            <ToggleOffIcon fontSize="small" />
                        ) : (
                            <ToggleOnIcon fontSize="small" />
                        )}
                    </IconButton>
                </Stack>
            ),
        },
    ];

    // ============================================================
    // RENDER
    // ============================================================
    return (
        <Container maxWidth="xl" sx={{ mt: 4 }}>
            {/* LOADER FULLSCREEN */}
            <Backdrop open={loading} sx={{ zIndex: 99999 }}>
                <CircularProgress color="inherit" />
            </Backdrop>

            {/* SNACKBAR */}
            <Snackbar
                open={snack.open}
                autoHideDuration={4000}
                onClose={() => setSnack({ ...snack, open: false })}
                anchorOrigin={{ vertical: "top", horizontal: "center" }}
            >
                <Alert severity={snack.severity} variant="filled" sx={{ width: "100%" }}>
                    {snack.message}
                </Alert>
            </Snackbar>

            <Paper sx={{ p: 3, borderRadius: 3 }}>
                <Typography variant="h5" sx={{ mb: 2 }}>
                    📚 Base de Conhecimento
                </Typography>

                <Button
                    variant="contained"
                    startIcon={<AddIcon />}
                    sx={{ mb: 2 }}
                    onClick={() => openModal()}
                >
                    Novo Registro
                </Button>

                <TextField
                    fullWidth
                    label="Filtrar..."
                    sx={{ mb: 2 }}
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                />

                {/* DATAGRID */}
                <Box sx={{ height: 600 }}>
                    <DataGrid
                        rows={items.filter((x) => {
                            const q = search.toLowerCase();
                            return (
                                x.title.toLowerCase().includes(q) ||
                                x.section.toLowerCase().includes(q) ||
                                x.subsection.toLowerCase().includes(q) ||
                                x.keyName.toLowerCase().includes(q)
                            );
                        })}
                        columns={columns}
                        getRowId={(row) => row.id}
                        pageSizeOptions={[10, 20, 50]}
                        initialState={{
                            pagination: { paginationModel: { pageSize: 10 } },
                        }}
                        slots={{ toolbar: GridToolbar }}
                        sx={{
                            borderRadius: 2,
                            "& .MuiDataGrid-row:hover": {
                                backgroundColor: "rgba(0,0,0,0.04)",
                            },
                        }}
                    />
                </Box>
            </Paper>

            {/* MODAL ===================================================== */}
            <Modal open={open} onClose={closeModal}>
                <Box
                    sx={{
                        width: "95%",
                        maxWidth: 800,   // menor!
                        bgcolor: "background.paper",
                        borderRadius: 2,
                        p: 3,
                        position: "absolute",
                        top: "50%",
                        left: "50%",
                        transform: "translate(-50%, -50%)",
                        maxHeight: "85vh",
                        overflowY: "auto",
                    }}
                >
                    <Typography variant="h6" sx={{ mb: 3 }}>
                        {editingId ? "Editar Registro" : "Novo Registro"}
                    </Typography>

                    <Grid container spacing={2}>
                        <Grid item xs={12} md={6}>
                            <Typography>Seção</Typography>
                            <Select
                                fullWidth
                                value={section}
                                onChange={(e) => setSection(e.target.value)}
                                sx={{ mb: 2 }}
                            >
                                <MenuItem value="">
                                    <i>Selecionar</i>
                                </MenuItem>
                                {sections.map((s) => (
                                    <MenuItem key={s} value={s}>
                                        {s}
                                    </MenuItem>
                                ))}
                            </Select>

                            <Typography>Ou criar nova seção</Typography>
                            <TextField
                                fullWidth
                                placeholder="Ex: Referência 1"
                                sx={{ mb: 2 }}
                                value={newSection}
                                onChange={(e) => setNewSection(e.target.value)}
                            />

                            <Typography>Subseção</Typography>
                            <TextField
                                fullWidth
                                sx={{ mb: 2 }}
                                value={subsection}
                                onChange={(e) => setSubsection(e.target.value)}
                            />

                            <Typography>Título</Typography>
                            <TextField
                                fullWidth
                                sx={{ mb: 2 }}
                                value={title}
                                onChange={(e) => setTitle(e.target.value)}
                            />

                            <Typography>KeyName</Typography>
                            <TextField
                                fullWidth
                                sx={{ mb: 2 }}
                                value={keyName}
                                onChange={(e) => setKeyName(e.target.value)}
                            />
                        </Grid>

                        <Grid item xs={12} md={6}>
                            <Typography>Editor Markdown</Typography>
                            <TextField
                                fullWidth
                                multiline
                                minRows={12}
                                sx={{
                                    mb: 2,
                                    fontFamily: "monospace",
                                    bgcolor: "background.paper",
                                    color: "text.primary",
                                    maxHeight: 350,
                                    overflowY: "auto"
                                }}
                                value={content}
                                onChange={(e) => setContent(e.target.value)}
                            />

                            <Typography>Preview</Typography>
                            <Paper
                                sx={{
                                    p: 2,
                                    minHeight: 200,
                                    maxHeight: 350,
                                    overflowY: "auto",
                                }}
                            >
                                <div className="markdown-body">
                                    <ReactMarkdown>{content || "*Vazio*"}</ReactMarkdown>
                                </div>
                            </Paper>
                        </Grid>
                    </Grid>

                    <Stack direction="row" spacing={2} sx={{ mt: 3 }}>
                        <Button variant="contained" onClick={save} disabled={loading}>
                            Salvar
                        </Button>
                        <Button onClick={closeModal} disabled={loading}>
                            Cancelar
                        </Button>
                    </Stack>
                </Box>
            </Modal>
        </Container>
    );
}
