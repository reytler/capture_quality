// CaptureQuality App JavaScript

window.captureQuality = {
    _currentStream: null,
    _currentFacingMode: 'environment',

    initCamera: async function (videoElementId, canvasElementId, facingMode = 'environment') {
        console.log('[CaptureQuality] initCamera called:', videoElementId, canvasElementId, facingMode);
        console.log('[CaptureQuality] Location:', window.location.href);
        console.log('[CaptureQuality] Protocol:', window.location.protocol);
        console.log('[CaptureQuality] Host:', window.location.host);
        
        const video = document.getElementById(videoElementId);
        const canvas = document.getElementById(canvasElementId);
        
        if (!video || !canvas) {
            console.error('[CaptureQuality] Video or canvas element not found', { video: !!video, canvas: !!canvas });
            return { success: false, error: 'Elements not found' };
        }

        if (!navigator.mediaDevices) {
            console.error('[CaptureQuality] navigator.mediaDevices not available');
            return { success: false, error: 'MediaDevices not available' };
        }
        
        console.log('[CaptureQuality] navigator.mediaDevices available');
        console.log('[CaptureQuality] Permissions state:', navigator.permissions);

        // Stop existing stream
        if (this._currentStream) {
            this._currentStream.getTracks().forEach(track => track.stop());
            this._currentStream = null;
        }

        this._currentFacingMode = facingMode;

        // Try with facingMode first
        const constraints = {
            video: {
                facingMode: facingMode,
                width: { ideal: 1920 },
                height: { ideal: 1080 }
            }
        };

        try {
            console.log('[CaptureQuality] Trying getUserMedia with facingMode:', facingMode);
            
            const stream = await navigator.mediaDevices.getUserMedia(constraints);
            
            console.log('[CaptureQuality] Stream obtained successfully!');
            
            video.srcObject = stream;
            this._currentStream = stream;
            
            // play and wait for video to be ready
            await video.play();
            
            // Wait until video has dimensions (readyState >= 2)
            if (video.readyState < 2) {
                console.log('[CaptureQuality] Waiting for video ready...');
                await new Promise(resolve => {
                    video.onloadedmetadata = () => resolve();
                    setTimeout(() => resolve(), 2000); // timeout 2s
                });
            }
            
            console.log('[CaptureQuality] Video ready:', video.videoWidth, 'x', video.videoHeight, 'readyState:', video.readyState);
            
            // Configure canvas dimensions
            canvas.width = video.videoWidth || video.clientWidth || 640;
            canvas.height = video.videoHeight || video.clientHeight || 480;
            
            // Check if we can switch cameras
            const devices = await navigator.mediaDevices.enumerateDevices();
            const videoInputs = devices.filter(d => d.kind === 'videoinput');
            const canSwitch = videoInputs.length > 1;

            return {
                success: true,
                facingMode: facingMode,
                canSwitch: canSwitch,
                deviceId: stream.getVideoTracks()[0]?.getSettings()?.deviceId
            };
        } catch (err) {
            console.error('[CaptureQuality] Camera init failed:', err.name, err.message);
            return { success: false, error: err.message };
        }
    },

    switchCamera: async function (videoElementId, canvasElementId) {
        console.log('[CaptureQuality] switchCamera called');
        
        const newFacingMode = this._currentFacingMode === 'environment' ? 'user' : 'environment';
        console.log('[CaptureQuality] Switching from', this._currentFacingMode, 'to', newFacingMode);
        
        return await this.initCamera(videoElementId, canvasElementId, newFacingMode);
    },

    getCameraInfo: async function () {
        const devices = await navigator.mediaDevices.enumerateDevices();
        const videoInputs = devices.filter(d => d.kind === 'videoinput');
        
        return {
            facingMode: this._currentFacingMode,
            canSwitch: videoInputs.length > 1,
            deviceId: this._currentStream?.getVideoTracks()[0]?.getSettings()?.deviceId
        };
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
        if (this._currentStream) {
            this._currentStream.getTracks().forEach(track => track.stop());
            this._currentStream = null;
        }
        
        const video = document.getElementById(videoElementId);
        if (video) {
            video.srcObject = null;
        }
    },

    getImageBytes: async function (videoElementId, canvasElementId) {
        try {
            const video = document.getElementById(videoElementId);
            const canvas = document.getElementById(canvasElementId);

            if (!video || !canvas) {
                throw new Error('[CaptureQuality] getImageBytes: elements not found');
            }

            if (video.readyState < 2) {
                await new Promise((resolve, reject) => {
                    const timeout = setTimeout(() => {
                        reject(new Error('Timeout waiting for video to be ready.'));
                    }, 3000);

                    const checkReady = () => {
                        if (video.readyState >= 2) {
                            clearTimeout(timeout);
                            resolve();
                        } else {
                            requestAnimationFrame(checkReady);
                        }
                    };

                    checkReady();
                });
            }

            const width = video.videoWidth || video.clientWidth || 640;
            const height = video.videoHeight || video.clientHeight || 480;

            canvas.width = width;
            canvas.height = height;

            const ctx = canvas.getContext('2d');

            if (!ctx) {
                throw new Error('Could not get 2D context of canvas.');
            }

            ctx.drawImage(video, 0, 0, width, height);

            const blob = await new Promise((resolve, reject) => {
                canvas.toBlob(result => {
                    if (!result || result.size < 100) {
                        reject(new Error('Captured image is empty or invalid.'));
                        return;
                    }

                    resolve(result);
                }, 'image/png');
            });

            console.log('[CaptureQuality] Blob captured:', blob.size);

            return blob;

        } catch (error) {
            console.error('[CaptureQuality-ERROR] getImageBytes:', error);
            throw error;
        }
    }
};

console.log('[CaptureQuality] app.js loaded');