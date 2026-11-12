FROM python:3.12-slim@sha256:5e2bdb1b0d542d5039cffb8181c65930093949194a65c365dfb1b0fe13a4fa5b AS dataset
WORKDIR /build
COPY scripts/atlas_trino_load.py scripts/atlas_dataset.py ./scripts/
RUN python scripts/atlas_trino_load.py --export-sql /atlas.sql

FROM trinodb/trino:484@sha256:2704eb53935af5e582d63d6033e9bf99d20c325e9b0e30b1016ef59a4773979f
COPY --from=dataset --chown=1000:1000 /atlas.sql /opt/atlas/initialize.sql
COPY --chmod=755 trino/atlas/start.sh /opt/atlas/start.sh
ENTRYPOINT ["/bin/bash", "/opt/atlas/start.sh"]
CMD []
