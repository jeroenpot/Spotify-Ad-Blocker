﻿/*
  LICENSE
  -------
  Copyright (C) 2007-2010 Ray Molenkamp

  This source code is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this source code or the software it produces.

  Permission is granted to anyone to use this source code for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this source code must not be misrepresented; you must not
     claim that you wrote the original source code.  If you use this source code
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original source code.
  3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Runtime.InteropServices;
using CoreAudio.Interfaces;
#if (NET40)
#endif

namespace CoreAudio
{
    public class AudioSessionManager2 : IDisposable
    {
        public delegate void SessionCreatedDelegate(object sender, IAudioSessionControl2 newSession);

        private readonly IAudioSessionManager2 _AudioSessionManager2;
        private AudioSessionNotification _AudioSessionNotification;

        internal AudioSessionManager2(IAudioSessionManager2 realAudioSessionManager2)
        {
            _AudioSessionManager2 = realAudioSessionManager2;

            RefreshSessions();
        }

        public SessionCollection Sessions { get; private set; }

        public void Dispose()
        {
            UnregisterNotifications();
        }

        public event SessionCreatedDelegate OnSessionCreated;

        internal void FireSessionCreated(IAudioSessionControl2 newSession)
        {
            if (OnSessionCreated != null) OnSessionCreated(this, newSession);
        }

        public void RefreshSessions()
        {
            UnregisterNotifications();

            IAudioSessionEnumerator _SessionEnum;
            Marshal.ThrowExceptionForHR(_AudioSessionManager2.GetSessionEnumerator(out _SessionEnum));
            Sessions = new SessionCollection(_SessionEnum);

            _AudioSessionNotification = new AudioSessionNotification(this);
            Marshal.ThrowExceptionForHR(_AudioSessionManager2.RegisterSessionNotification(_AudioSessionNotification));
        }

        private void UnregisterNotifications()
        {
            if (Sessions != null)
                Sessions = null;

            if (_AudioSessionNotification != null)
                Marshal.ThrowExceptionForHR(
                    _AudioSessionManager2.UnregisterSessionNotification(_AudioSessionNotification));
        }

        ~AudioSessionManager2()
        {
            Dispose();
        }
    }
}