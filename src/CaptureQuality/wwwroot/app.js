// CaptureQuality App JavaScript

window.captureQuality = {
    initCamera: async function (videoElementId, canvasElementId) {
        console.log('[CaptureQuality] initCamera chamado:', videoElementId, canvasElementId);
        console.log('[CaptureQuality] Location:', window.location.href);
        console.log('[CaptureQuality] Protocol:', window.location.protocol);
        console.log('[CaptureQuality] Host:', window.location.host);
        
        const video = document.getElementById(videoElementId);
        const canvas = document.getElementById(canvasElementId);
        
        if (!video || !canvas) {
            console.error('[CaptureQuality] Video or canvas element not found', { video: !!video, canvas: !!canvas });
            return false;
        }

        // Verificar se navigator.mediaDevices está disponível
        if (!navigator.mediaDevices) {
            console.error('[CaptureQuality] navigator.mediaDevices não disponível');
            return false;
        }
        
        console.log('[CaptureQuality] navigator.mediaDevices disponível');
        console.log('[CaptureQuality] Permissions state:', navigator.permissions);

        // Tentar primeiro com constraints mínimas
        var constraints = {
            video: true
        };
        
        try {
            console.log('[CaptureQuality] Tentando getUserMedia com constraints:', constraints);
            
            const stream = await navigator.mediaDevices.getUserMedia(constraints);
            
            console.log('[CaptureQuality] Stream obtida com sucesso!');
            
            video.srcObject = stream;
            
            // play e esperar video estar pronto
            await video.play();
            
            // Esperar até video ter dimensões (readyState >= 2)
            if (video.readyState < 2) {
                console.log('[CaptureQuality] Aguardando video ready...');
                await new Promise(resolve => {
                    video.onloadedmetadata = () => resolve();
                    setTimeout(() => resolve(), 2000); // timeout 2s
                });
            }
            
            console.log('[CaptureQuality] Video ready:', video.videoWidth, 'x', video.videoHeight, 'readyState:', video.readyState);
            
            // Configurar dimensions do canvas
            canvas.width = video.videoWidth || video.clientWidth || 640;
            canvas.height = video.videoHeight || video.clientHeight || 480;
            
            return true;
        } catch (err) {
            console.error('[CaptureQuality] getUserMedia error:', err.name, err.message);
            throw new Error("Camera not found: " + err.message);
        }
    },

    captureFrame: function (videoElementId, canvasElementId) {
        const video = document.getElementById(videoElementId);
        const canvas = document.getElementById(canvasElementId);
        
        if (!video || !canvas) {
            console.error('[CaptureQuality] captureFrame: video or canvas not found');
            return null;
        }

        console.log('[CaptureQuality] Drawing frame:', video.readyState, video.videoWidth);
        
        if (video.readyState >= 2) {
            canvas.width = video.videoWidth || 640;
            canvas.height = video.videoHeight || 480;
            const ctx = canvas.getContext('2d');
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            console.log('[CaptureQuality] Frame drawn:', canvas.width, 'x', canvas.height);
        }
        
        return canvas.toDataURL('image/png');
    },

    stopCamera: function (videoElementId) {
        const video = document.getElementById(videoElementId);
        if (video && video.srcObject) {
            video.srcObject.getTracks().forEach(track => track.stop());
            video.srcObject = null;
        }
    },

    // CRITICAL FIX: Made async to properly await from Blazor
    getImageData: async function (videoElementId, canvasElementId) {
        const video = document.getElementById(videoElementId);
        const canvas = document.getElementById(canvasElementId);
        
        if (!video || !canvas) {
            console.error('[CaptureQuality] getImageData: elements not found');
            return null;
        }

        // CRITICAL FIX: Use async waiting instead of blocking loop
        // Wait for video to be ready (max 1 second)
        if (video.readyState < 2) {
            console.log('[CaptureQuality] Waiting for video ready...', video.readyState);
            await new Promise(resolve => {
                const checkReady = () => {
                    if (video.readyState >= 2) {
                        resolve();
                    } else {
                        // Use requestAnimationFrame for non-blocking wait
                        requestAnimationFrame(checkReady);
                    }
                };
                checkReady();
            });
        }
        
        // Additional small delay to ensure frame is fully rendered
        await new Promise(resolve => setTimeout(resolve, 10));
        
        const width = video.videoWidth || video.clientWidth || 640;
        const height = video.videoHeight || video.clientHeight || 480;
        
        canvas.width = width;
        canvas.height = height;
        
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0, width, height);
        
        var dataUrl = canvas.toDataURL('image/png');
        
        // Verificar se imagem tem conteúdo
        if (!dataUrl || dataUrl.length < 100) {
            console.error('[CaptureQuality] Imagem vazia:', dataUrl ? dataUrl.length : 'null');
            return null;
        }
        
        console.log('[CaptureQuality] Image captured:', dataUrl.substring(0, 60), '...len:', dataUrl.length);
        return dataUrl;
    }
};

console.log('[CaptureQuality] app.js carregado');
