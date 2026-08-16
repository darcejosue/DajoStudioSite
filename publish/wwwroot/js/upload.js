/* ==========================================================================
   DajoStudio UpdateServer - 250MB+ Upload Engine with Live Progress
   ========================================================================== */

document.addEventListener('DOMContentLoaded', () => {
  const dropzone = document.getElementById('uploadDropzone');
  const fileInput = document.getElementById('fileInput');
  const uploadForm = document.getElementById('uploadForm');
  const fileInfoCard = document.getElementById('fileInfoCard');
  const fileNameDisplay = document.getElementById('fileNameDisplay');
  const fileSizeDisplay = document.getElementById('fileSizeDisplay');
  const removeFileBtn = document.getElementById('removeFileBtn');
  
  const progressBox = document.getElementById('progressBox');
  const progressBarFill = document.getElementById('progressBarFill');
  const progressPercentage = document.getElementById('progressPercentage');
  const progressTransferred = document.getElementById('progressTransferred');
  const progressSpeed = document.getElementById('progressSpeed');
  const progressTimeRemaining = document.getElementById('progressTimeRemaining');
  const submitBtn = document.getElementById('submitBtn');
  const statusMessage = document.getElementById('statusMessage');

  if (!dropzone || !fileInput || !uploadForm) return;

  // Drag & Drop event handlers
  ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
    dropzone.addEventListener(eventName, preventDefaults, false);
  });

  function preventDefaults(e) {
    e.preventDefault();
    e.stopPropagation();
  }

  ['dragenter', 'dragover'].forEach(eventName => {
    dropzone.addEventListener(eventName, () => dropzone.classList.add('dragover'), false);
  });

  ['dragleave', 'drop'].forEach(eventName => {
    dropzone.addEventListener(eventName, () => dropzone.classList.remove('dragover'), false);
  });

  dropzone.addEventListener('drop', (e) => {
    const dt = e.dataTransfer;
    const files = dt.files;
    if (files.length > 0) {
      fileInput.files = files;
      handleFileSelected(files[0]);
    }
  });

  dropzone.addEventListener('click', () => {
    fileInput.click();
  });

  fileInput.addEventListener('change', () => {
    if (fileInput.files.length > 0) {
      handleFileSelected(fileInput.files[0]);
    }
  });

  removeFileBtn?.addEventListener('click', (e) => {
    e.stopPropagation();
    fileInput.value = '';
    fileInfoCard.style.display = 'none';
    dropzone.style.display = 'block';
  });

  function handleFileSelected(file) {
    fileNameDisplay.textContent = file.name;
    fileSizeDisplay.textContent = formatBytes(file.size);
    dropzone.style.display = 'none';
    fileInfoCard.style.display = 'flex';
  }

  function formatBytes(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  // Upload Form Submit via XMLHttpRequest for real-time progress
  uploadForm.addEventListener('submit', (e) => {
    e.preventDefault();

    if (!fileInput.files || fileInput.files.length === 0) {
      showError('Por favor selecciona un archivo de actualización para subir.');
      return;
    }

    const formData = new FormData(uploadForm);
    const xhr = new XMLHttpRequest();

    let startTime = Date.now();
    let prevLoaded = 0;

    // Reset progress UI
    progressBox.style.display = 'block';
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<i class="bi bi-arrow-repeat spin"></i> Procesando y Calculando Hash...';
    statusMessage.style.display = 'none';
    progressBarFill.style.width = '0%';
    progressPercentage.textContent = '0%';

    // Track upload progress
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) {
        const percent = Math.round((e.loaded / e.total) * 100);
        progressBarFill.style.width = percent + '%';
        progressPercentage.textContent = percent + '%';

        // Calculate size transferred
        const loadedFormatted = formatBytes(e.loaded);
        const totalFormatted = formatBytes(e.total);
        progressTransferred.textContent = `${loadedFormatted} / ${totalFormatted}`;

        // Calculate speed & ETA
        const currentTime = Date.now();
        const durationSec = (currentTime - startTime) / 1000;
        
        if (durationSec > 0.5) {
          const speedBytesPerSec = e.loaded / durationSec;
          const speedMB = (speedBytesPerSec / (1024 * 1024)).toFixed(2);
          progressSpeed.textContent = `${speedMB} MB/s`;

          const remainingBytes = e.total - e.loaded;
          const remainingSec = Math.round(remainingBytes / speedBytesPerSec);
          
          if (remainingSec < 60) {
            progressTimeRemaining.textContent = `${remainingSec}s restantes`;
          } else {
            const mins = Math.floor(remainingSec / 60);
            const secs = remainingSec % 60;
            progressTimeRemaining.textContent = `${mins}m ${secs}s restantes`;
          }
        }
      }
    };

    xhr.onload = () => {
      submitBtn.disabled = false;
      submitBtn.innerHTML = '<i class="bi bi-cloud-arrow-up-fill"></i> Publicar Actualización';

      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const res = JSON.parse(xhr.responseText);
          if (res.success) {
            progressPercentage.textContent = '100% (Completado)';
            progressTimeRemaining.textContent = '¡Completado con éxito!';
            showSuccess(res.message || 'Actualización publicada exitosamente.');
            setTimeout(() => {
              window.location.href = '/Home/Index';
            }, 1500);
          } else {
            showError(res.message || 'Error al procesar la subida.');
          }
        } catch (err) {
          window.location.href = '/Home/Index';
        }
      } else {
        try {
          const res = JSON.parse(xhr.responseText);
          showError(res.message || `Error del servidor (${xhr.status}).`);
        } catch (err) {
          showError(`Ocurrió un error inesperado al subir el archivo (${xhr.status}).`);
        }
      }
    };

    xhr.onerror = () => {
      submitBtn.disabled = false;
      submitBtn.innerHTML = '<i class="bi bi-cloud-arrow-up-fill"></i> Publicar Actualización';
      showError('Error de red al intentar conectar con el servidor.');
    };

    xhr.open('POST', '/Home/Upload', true);
    xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
    xhr.send(formData);
  });

  function showError(msg) {
    statusMessage.className = 'alert alert-danger-custom mt-3';
    statusMessage.innerHTML = `<i class="bi bi-exclamation-triangle-fill"></i> ${msg}`;
    statusMessage.style.display = 'block';
  }

  function showSuccess(msg) {
    statusMessage.className = 'alert alert-success-custom mt-3';
    statusMessage.innerHTML = `<i class="bi bi-check-circle-fill"></i> ${msg}`;
    statusMessage.style.display = 'block';
  }
});
