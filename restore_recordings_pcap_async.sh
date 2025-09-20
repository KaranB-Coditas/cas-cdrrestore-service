#!/bin/bash

# Max concurrent jobs (tune based on server capacity & network bandwidth)
MAX_JOBS=5

restore_recordings() {
  if [ $(($# % 2)) -ne 0 ]; then
    echo "Usage: $0 <date_path1> <pcap_file1> [<date_path2> <pcap_file2> ...]"
    exit 1
  fi

  while [ $# -gt 0 ]; do
    DATE_PATH=$1   # e.g. 2025/09/20/11/15/SIP/sip_2025-09-20-11-15.tar[.gz]
    PCAP_NAME=$2   # e.g. 123456789.pcap
    shift 2

    (
      D_PATH=$(dirname "${DATE_PATH}")
      FILE_NAME=$(basename "${DATE_PATH}")   # sip_2025-09-20-11-15.tar[.gz]
      TAR_NAME="${FILE_NAME}"

      cd /var/spool/voipmonitor || exit 1
      sudo mkdir -p "${D_PATH}"

      TMP_BASE="/tmp/voip_restore"
      mkdir -p "$TMP_BASE"
      TMP_DIR=$(mktemp -d -p "$TMP_BASE")

      # Detect tar type: .tar or .tar.gz
      if [[ "${DATE_PATH}" == *.tar.gz ]]; then
        TAR_CMD="tar -xOzf - \"${PCAP_NAME}\""
      else
        TAR_CMD="tar -xOf - \"${PCAP_NAME}\""
      fi

      # --- Stream fetch from GCS and extract only the requested PCAP ---
      if eval "gsutil cat \"gs://cas-pcap-recordings/${DATE_PATH}\" | ${TAR_CMD} > \"${TMP_DIR}/${PCAP_NAME}\"" 2> "${TMP_DIR}/error.log"; then

        # Repack extracted PCAP into tar with original tar name
        if [[ "${DATE_PATH}" == *.tar.gz ]]; then
          sudo tar -czf "${D_PATH}/${TAR_NAME}" -C "${TMP_DIR}" "${PCAP_NAME}"
        else
          sudo tar -cf "${D_PATH}/${TAR_NAME}" -C "${TMP_DIR}" "${PCAP_NAME}"
        fi

        echo "{\"tar\":\"${DATE_PATH}\",\"status\":\"success\",\"file\":\"${PCAP_NAME}\"}"
      else
        if grep -qi "Not found" "${TMP_DIR}/error.log"; then
          echo "{\"tar\":\"${DATE_PATH}\",\"status\":\"not_found\",\"file\":\"${PCAP_NAME}\"}"
        else
          echo "{\"tar\":\"${DATE_PATH}\",\"status\":\"fail\",\"reason\":\"$(tr -d '\"' < "${TMP_DIR}/error.log")\"}"
        fi
      fi

      rm -rf "${TMP_DIR}"
    ) &

    # Concurrency throttle
    while [ "$(jobs -r | wc -l)" -ge "$MAX_JOBS" ]; do
      sleep 1
    done
  done

  wait
}

restore_recordings "$@"
