// CaptureQuality App JavaScript

window.captureQuality = {
    initCamera: async function (videoElementId, canvasElementId) => {
        const video = document.getElementById(videoElementId);
        const canvas = document.getElementById(canvasElementId);
        
        if (!video || !canvas) {
            console.error('Video or canvas element not found');
            return false;
        }

        try {
            const stream = await navigator.mediaDevices.getUserMedia({
                video: { 
                    facingMode: 'environment',
                    width: { ideal: 1920 },
                    height: { ideal: 1080 }
                }
            });
            
            video.srcObject = stream;
            await video.play();
            
            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            
            return true;
        } catch (err) {
            console.error('Camera access error:', err);
            return false;
        }
    },

    captureFrame: function (videoElementId, canvasElementId) {
        const video = document.getElementById(videoElementId);
        const canvas = document.getElementById(canvasElementId);
        
        if (!video || !canvas) {
            return null;
        }

        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        
        return canvas.toDataURL('image/png');
    },

    stopCamera: function (videoElementId) {
        const video = document.getElementById(videoElementId);
        if (video && video.srcObject) {
            video.srcObject.getTracks().forEach(track => track.stop());
            video.srcObject = null;
        }
    },

    getImageData: function (canvasElementId) {
        const canvas = document.getElementById(canvasElementId);
        if (!canvas) return null;
        
        return canvas.toDataURL('image/png');
    }
};